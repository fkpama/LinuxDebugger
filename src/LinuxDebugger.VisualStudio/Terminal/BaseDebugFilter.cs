using System.Text;
using liblinux.Shell;
using Microsoft.VisualStudio.Linux.Package;
using Microsoft.VisualStudio.Linux.Shared;

namespace LinuxDebugger.VisualStudio.Terminal
{
    internal abstract class BaseDebugFilter : TextFilter, IDebugFilter
    {
        protected readonly LinuxConsolePtyProxy linuxConsole;
        private byte[]? forOutput;
        public const byte LF = 10;
        public const byte CR = 13;
        public const byte CTRLC = 3;

        public TextFilter? TextFilter { get; set; }

        public string? Tty { get; protected set; }

        protected BaseDebugFilter(LinuxConsolePtyProxy linuxConsole)
        {
            this.linuxConsole = linuxConsole;
        }

        public virtual async Task SendCtrlCAsync(IStreamingShell _) => await this.linuxConsole.SendCtrlCAsync();

        public virtual (byte[]? outBuffer, int n) FilterOutputStream(byte[]? buffer, int count)
        {
            if (this.TextFilter != null)
            {
                this.forOutput = null;
                this.TextFilter.Filter(Encoding.UTF8.GetString(buffer, 0, count));
                var forOutput = this.forOutput;
                count = this.forOutput == null ? 0 : this.forOutput.Length;
                buffer = forOutput;
            }
            return (buffer, count);
        }

        protected override void FilterImpl(string text)
                => this.forOutput = Encoding.UTF8.GetBytes(text);

        public abstract Task<IShell> SetupShellAsync(ShellCreateOptions options,
                                                      PtyDebugProxy proxy);
    }
}
