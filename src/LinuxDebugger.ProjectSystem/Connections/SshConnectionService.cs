using LinuxDebugger.ProjectSystem.Controls;
using LinuxDebugger.ProjectSystem.ViewModels;
using LinuxDebugger.VisualStudio;

namespace LinuxDebugger.ProjectSystem
{
    [Export(ExportContractNames.Scopes.ProjectService, typeof(ISshConnectionService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class SshConnectionService : ISshConnectionService
    {
        public TerminalManager TerminalManager { get; }

        private readonly IProjectThreadingService threadingService;
        private readonly ILaunchSettingsProvider3 launchSettingsProvider;


        public VsConnectionManager Manager { get; }

        [ImportingConstructor]
        public SshConnectionService(IProjectThreadingService threadingService,
            ILaunchSettingsProvider launchSettingsProvider,
            [Import(typeof(SVsServiceProvider))]IAsyncServiceProvider2 services,
            LoggerService logService)
        {
            this.Manager = new(threadingService.JoinableTaskFactory);
            //var logger = logService.GetLogger<TerminalManager>();
            this.TerminalManager = new TerminalManager(services,
                                                       threadingService.JoinableTaskFactory,
                                                       this.Manager);
            this.threadingService = threadingService;
            this.launchSettingsProvider = (ILaunchSettingsProvider3)launchSettingsProvider;
        }

        public async Task<IVsSshClient> GetConnectionAsync(ILaunchProfile profile, bool force, CancellationToken cancellationToken)
        {
            var connId = profile.GetConnectionId();
            if (!connId.HasValue)
            {
                return await this.Manager
                    .GetDefaultConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                return await this.Manager
                    .GetConnectionAsync(connId.Value, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (KeyNotFoundException) when(force)
            {
            }

            var connections = await this.Manager
                    .GetConnectionInfosAsync(cancellationToken)
                    .ConfigureAwait(false);
            await this.threadingService.SwitchToUIThread();
            var vms = new ConnectionSelectionViewModel(connections);
            var window = new ConnectionSelectionWindow { DataContext = vms, };
            var ret = window.ShowDialog();
            if (ret is null || !ret.Value)
            {
                throw new KeyNotFoundException();
            }

            var selected = vms.Connections
                .Where(x => x.IsChecked)
                .Select(x => x.ConnectionInfo)
                .Single();
            Assumes.Present(selected);
            Assumes.Present(profile.Name);
            await launchSettingsProvider
                .TryUpdateProfileAsync(profile.Name,
                                       x => x.SetConnectionId(selected.Id))
                .ConfigureAwait(false);
            var client = await this.Manager
                .GetConnectionAsync(int.Parse(selected.Id), cancellationToken)
                .ConfigureAwait(false);
            Assumes.Present(client);
            return client;
        }

        public async ValueTask<string> OpenTtyAsync(ILaunchProfile profile,
            IVsSshClient client,
            CancellationToken cancellationToken)
        {
            return await this.TerminalManager
                .OpenTtyAsync(client.ConnectionInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        public async ValueTask<string> OpenTtyAsync(ILaunchProfile profile, CancellationToken cancellationToken)
        {
            var connectionId = profile.GetConnectionId();
            if (!connectionId.HasValue)
                throw new InvalidOperationException();
            var infos = await this.Manager
                .GetConnectionInfoAsync(connectionId.Value, cancellationToken)
                .ConfigureAwait(false);
            return await this.TerminalManager
                .OpenTtyAsync(infos, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
