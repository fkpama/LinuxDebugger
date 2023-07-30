using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using liblinux.Shell;
using Microsoft.VisualStudio.Linux.Package;
using Microsoft.VisualStudio.Utilities;

namespace LinuxDebugger.VisualStudio.Terminal
{
    internal class SSHGdbDebugFilter : BaseDebugFilter
    {
        private const string TtyCmd = "tty";
        private const string _ttyStartCommandSentinel = "E2E32F1A-3E99-4EDF-A62A-10EDB5F81201";
        private const string _ttyEndCommandSentinel = "3DF461C2-CE92-4D65-BA28-5278E01CFF5A";
        private const string sleepForeverCommand = "sleep 31536000";
        private bool processedFirstLine;
        private bool skipToEndOfLine;
        private bool seenHalf;

        private const string _ttyCmd = $"echo '{_ttyStartCommandSentinel}'; {TtyCmd}; echo '{_ttyEndCommandSentinel}'";
        private static readonly Regex s_sentinelRegex = new($@"[^']?{_ttyStartCommandSentinel}[\s]*(?<devicetty>\/dev[\/A-Za-z0-9]*\/[A-Za-z0-9]+)[\s]*{_ttyEndCommandSentinel}", RegexOptions.Multiline | RegexOptions.Compiled);
        private const string sleepforever = nameof(sleepforever);

        public SSHGdbDebugFilter(LinuxConsolePtyProxy proxy)
          : base(proxy)
        {
        }

        private string? GetStringFromBuffer(byte[] buffer, int count)
                => count <= 0 ? null : Encoding.UTF8.GetString(buffer, 0, count);

        private int FindEOL(byte[] buffer, int count)
        {
            if (count <= 0)
                return 0;
            if (this.seenHalf && buffer[0] == '\n')
            {
                this.seenHalf = false;
                return 1;
            }
            this.seenHalf = false;
            for (var index = 0; index < count; ++index)
            {
                if (buffer[index] == '\r')
                {
                    if (index == count - 1)
                    {
                        this.seenHalf = true;
                        return 0;
                    }
                    if (buffer[index + 1] == '\n')
                        return index + 2;
                }
            }
            return 0;
        }

        public override (byte[]? outBuffer, int n) FilterOutputStream(byte[]? buffer, int count)
        {
            if (buffer is null)
                return (buffer, count);
            if (!this.processedFirstLine)
            {
                var stringFromBuffer = this.GetStringFromBuffer(buffer, count);
                if (stringFromBuffer != null)
                    this.skipToEndOfLine = stringFromBuffer.StartsWith("&\"warn");
                else
                    this.processedFirstLine = true;
                this.processedFirstLine = true;
            }
            if (this.skipToEndOfLine)
            {
                var eol = this.FindEOL(buffer, count);
                if (eol != 0)
                {
                    this.skipToEndOfLine = false;
                    var array =  buffer.Skip(eol).ToArray();
                    count -= eol;
                    buffer = array;
                }
                else
                    count = 0;
            }
            return base.FilterOutputStream(buffer, count);
        }

        public override async Task<IShell> SetupShellAsync(
          ShellCreateOptions options,
          PtyDebugProxy proxy)
        {
            var shell = proxy.System.CreateShell(options);
            var cancellationToken = VsShellUtilities.ShutdownToken;
            var sshttyDeviceAsync = await this
                .GetSSHTTYDeviceAsync(shell, cancellationToken)
                .ConfigureAwait(false);
            this.Tty = sshttyDeviceAsync;
            return shell;
        }

        private async Task<string> GetSSHTTYDeviceAsync(IShell shell, CancellationToken cancellationToken)
        {
            string? ttyIdentified =  null;
            var outputOfTTYCommand = string.Empty;
            object lockObject = shell;
            PooledStringBuilder? sb = null;
            try
            {
                shell.OutputReceived += ShellTTYCommandReceivedCallback;
                _ = await shell
                    .WaitOutputReceivedAsync(TimeSpan.FromSeconds(10.0), cancellationToken)
                    .ConfigureAwait(false);
                if (!await this.RetryFunctionUntilSuccessOrTimeoutAsync(async () =>
                {
                    shell.WriteLine(_ttyCmd);
                    shell.Flush();
                    _ = await shell
                    .WaitOutputReceivedAsync($"\n{_ttyEndCommandSentinel}",
                                             TimeSpan.FromSeconds(10.0),
                                             TimeSpan.FromSeconds(0.5),
                                             cancellationToken)
                    .ConfigureAwait(false);
                    var match = s_sentinelRegex.Match(outputOfTTYCommand);
                    if (match.Success)
                        ttyIdentified = match.Groups["devicetty"].Value;
                    return match.Success;
                }, TimeSpan.FromSeconds(15.0), cancellationToken))
                    throw new ApplicationException("Failed to create tty");
                lock (lockObject)
                {
                    sb = PooledStringBuilder.GetInstance();
                }
                shell.WriteLine($"echo -e '{sleepforever}'; {sleepForeverCommand}");
                shell.Flush();
                _ = await shell.WaitOutputReceivedAsync($"\r{sleepforever}",
                    TimeSpan.FromSeconds(10.0),
                    TimeSpan.FromSeconds(0.5),
                    sb.Builder,
                    2,
                    cancellationToken);
                var endAsync = await shell.ReadToEndAsync();
            }
            finally
            {
                sb?.Free();
                shell.OutputReceived -= ShellTTYCommandReceivedCallback;
            }
            return ttyIdentified ?? throw new ApplicationException("Failed to create tty");

            void ShellTTYCommandReceivedCallback(object sender, OutputReceivedEventArgs e)
            {
                lock (lockObject)
                {
                    if (sb is not null)
                        _ = sb.Builder.Append(e.Output);
                    else
                        outputOfTTYCommand += e.Output;
                }
            }
        }

        private async Task<bool> RetryFunctionUntilSuccessOrTimeoutAsync(
      Func<Task<bool>> task,
      TimeSpan timeout,
      CancellationToken cancellationToken)
        {
            var start = Stopwatch.GetTimestamp();
            while ((Stopwatch.GetTimestamp() - start) < timeout.Ticks)
            {
                if (await task().ConfigureAwait(false))
                    return true;
                await Task.Delay(TimeSpan.FromMilliseconds(50.0), cancellationToken).ConfigureAwait(true);
            }
            return false;
        }

    }
}
