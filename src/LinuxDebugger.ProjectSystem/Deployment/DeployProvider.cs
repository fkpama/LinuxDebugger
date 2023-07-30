using Microsoft.VisualStudio.ProjectSystem.Build;

namespace LinuxDebugger.ProjectSystem.Deployment
{
    [Export(typeof(IDeployProvider))]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    sealed class DeployProvider : IDeployProvider
    {
        private readonly ISshConnectionService connectionService;
        private readonly ILinuxDebuggerSettingsManager settingsManager;
        private readonly ILaunchSettingsProvider settingsProvider;

        public bool IsDeploySupported
        {
            get => string.Equals(this.settingsProvider
                .ActiveProfile?
                .CommandName, Constants.CommandName, StringComparison.Ordinal);
        }

        public ConfiguredProject Project { get; }

        [ImportingConstructor]
        public DeployProvider(ConfiguredProject project,
                              ILinuxDebuggerSettingsManager settingsManager,
                              [Import(ExportContractNames.Scopes.ProjectService)]
                              ISshConnectionService connectionService,
                              ILaunchSettingsProvider settingsProvider)
        {
            this.Project = project;
            this.connectionService = connectionService;
            this.settingsManager = settingsManager;
            this.settingsProvider = settingsProvider;
        }

        public void Commit() { }

        public async Task DeployAsync(CancellationToken cancellationToken, TextWriter outputPaneWriter)
        {
            var profile = this.settingsProvider.ActiveProfile;
            Assumes.NotNull(profile);

            var connection = await this.connectionService
                .GetConnectionAsync(profile, true, cancellationToken)
                .ConfigureAwait(false);

            var settings = await this.settingsManager
                .GetSettingsAsync(cancellationToken)
                .ConfigureAwait(false);
            var helper = new PropertyHelper(this.Project, settings, sshClient: connection);

            await helper.DeployAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        public void Rollback() { }
    }
}
