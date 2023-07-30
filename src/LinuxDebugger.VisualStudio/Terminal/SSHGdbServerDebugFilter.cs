using System.Text;
using liblinux.Services;
using liblinux.Shell;
using Microsoft.VisualStudio.Linux.Package;

namespace LinuxDebugger.VisualStudio.Terminal;

internal sealed class SSHGdbServerDebugFilter : BaseDebugFilter
{
    private readonly GdbServer gdbserver;
    private readonly byte[] endSentinel;
    private bool discardRemainingBytes;

    public SSHGdbServerDebugFilter(LinuxConsolePtyProxy linuxConsole, GdbServer gdbserver)
      : base(linuxConsole)
    {
        this.gdbserver = gdbserver;
        this.endSentinel = Encoding.UTF8.GetBytes(gdbserver.EndSentinel);
    }

    public override (byte[]? outBuffer, int n) FilterOutputStream(byte[]? buffer, int count)
    {
        if (buffer is null)
            return (buffer, count);
        if (this.discardRemainingBytes)
            return (null, 0);
        if (TryFindBytes(this.endSentinel, buffer, ref count))
            this.discardRemainingBytes = true;
        return base.FilterOutputStream(buffer, count);
    }

    public override Task<IShell?> SetupShellAsync(
      ShellCreateOptions options,
      PtyDebugProxy proxy)
    {
        this.gdbserver.TunnelExceptionThrown += new EventHandler<ExceptionEventArgs>(this.OnTunnelExceptionThrown);
        return TaskResult.Null<IShell>();
    }

    private void OnTunnelExceptionThrown(object sender, ExceptionEventArgs args)
    {
        if (args == null || args.Exception?.Message == null || !args.Exception.Message.Contains("Verify that TCP forwarding is enabled"))
            return;
        //LinuxRsyncFailureTelemetry.LogLinuxRsyncFailure(LinuxRsyncFailureTelemetry.RsyncFailureCode.TCPForwardingDisabled);
    }

    private static bool TryFindBytes(byte[] pattern, byte[] buffer, ref int count)
    {
        if (count <= 0)
            return false;
        var num = -1;
        for (var index1 = 0; index1 < count && num < 0; ++index1)
        {
            num = index1;
            for (var index2 = 0; index2 < pattern.Length; ++index2)
            {
                if (index1 + index2 >= count)
                    return false;
                if (buffer[index1 + index2] != pattern[index2])
                {
                    num = -1;
                    break;
                }
            }
        }
        if (num < 0)
            return false;
        count = num;
        return true;
    }
}
