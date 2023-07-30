using LinuxDebugger.VisualStudio;

namespace LinuxDebugger.ProjectSystem
{
    internal interface ISshConnectionService
    {
        VsConnectionManager Manager { get; }

        Task<IVsSshClient> GetConnectionAsync(ILaunchProfile profile, bool force, CancellationToken cancellationToken);
        ValueTask<string> OpenTtyAsync(ILaunchProfile profile,
                                       CancellationToken cancellationToken);
        ValueTask<string> OpenTtyAsync(ILaunchProfile profile,
            IVsSshClient client,
                                       CancellationToken cancellationToken);
    }
}
