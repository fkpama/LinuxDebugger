using System.Collections.Concurrent;
using EnvDTE;
using LinuxDebugger.VisualStudio.Terminal;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Terminal;

namespace LinuxDebugger.VisualStudio
{
    public sealed class TerminalManager
    {
        private readonly struct ConnectionInfo
        {
            public string Pty { get; }
            public Guid TerminalId { get; }
            public ConnectionInfo(string pty, Guid terminalId)
            {
                this.Pty = pty;
                this.TerminalId = terminalId;
            }
        }
        private readonly ConcurrentDictionary<int, Task<LinuxConsolePtyProxy>> connections = new();
        private readonly AsyncLazy<ITerminalService> terminalService;
        private readonly VsConnectionManager connectionManager;
        private readonly IAsyncServiceProvider services;
        private readonly JoinableTaskFactory taskFactory;

        public TerminalManager(IAsyncServiceProvider services,
                               JoinableTaskFactory taskFactory,
                               VsConnectionManager manager)
        {
            this.terminalService = services.GetTerminalService(taskFactory);
            this.connectionManager = manager;
            this.services = services;
            this.taskFactory = taskFactory;
        }

        public async Task<string> OpenTtyAsync(SshConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            try
            {
                var item = await this.connections
                .AddOrUpdate(connectionInfo.InternalId,
                async x =>
                {
                    var remoteSystem = await this
                    .connectionManager
                    .GetRemoteSystemAsync(connectionInfo, cancellationToken)
                    .ConfigureAwait(false);
                    var terminalService = await this.terminalService
                    .GetValueAsync(cancellationToken).ConfigureAwait(false);
                    var debugger = await this.services
                    .GetServiceAsync<SVsShellDebugger, IVsDebugger>()
                    .ConfigureAwait(false);
                    var dte = await this.services
                    .GetServiceAsync<SDTE, DTE>()
                    .ConfigureAwait(false);
                    var uiShell = await this.services
                    .GetServiceAsync<SVsUIShell, IVsUIShell>()
                    .ConfigureAwait(false);
                    await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
                    var factory = new LinuxConsolePtyProxy(terminalService,
                        connectionInfo,
                        dte,
                        uiShell,
                        debugger,
                        this.taskFactory);
                    factory.TerminalClosed += (o, e) => this.connections.TryRemove(connectionInfo.InternalId, out _);
                    //await this.taskFactory
                    //.RunAsync(VsTaskRunContext.UIThreadNormalPriority,
                    //async () =>
                    //{
                    _ = await factory
                        .ConnectAsync(cancellationToken)
                        .ConfigureAwait(false);
                    //});
                    return factory;
                }, (id, existing) => existing);

                await item
                    .ResetAsync(connectionInfo, cancellationToken)
                    .ConfigureAwait(false);
                await item.ClearScreenAsync(cancellationToken).ConfigureAwait(false);
                return item.Tty;
            }
            catch
            {
                _ = this.connections.TryRemove(connectionInfo.InternalId, out _);
                throw;
            }
        }
    }
}
