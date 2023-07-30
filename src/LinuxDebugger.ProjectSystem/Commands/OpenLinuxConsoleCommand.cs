namespace LinuxDebugger.ProjectSystem.Commands
{
    [Export(typeof(IAsyncCommandGroupHandler))]
    [ExportCommandGroup(LinuxCommands.LinuxCommandSetIdString)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class OpenLinuxConsoleCommand : IAsyncCommandGroupHandler
    {
        private readonly ILaunchSettingsProvider launchSettingsProvider;
        Lazy<ISshConnectionService> connectionService;
        public ConfiguredProject Project { get; }

        [ImportingConstructor]
        public OpenLinuxConsoleCommand(ConfiguredProject project,
                                       ILaunchSettingsProvider launchSettingsProvider,
                                       [Import(ExportContractNames.Scopes.ProjectService)]
                                       Lazy<ISshConnectionService> sshConnectionService)
            //[Import(typeof(SVsServiceProvider))]IAsyncServiceProvider services)
        {
            this.Project = project;
            this.connectionService = sshConnectionService;
            this.launchSettingsProvider = launchSettingsProvider;
        }

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes,
                                                               long commandId,
                                                               bool focused,
                                                               string? commandText,
                                                               CommandStatus progressiveStatus)
        {
            if (commandId == LinuxCommands.cmdidOpenLinuxConsole)
            {
                return getOpenConsoleStatusAsync(commandText, progressiveStatus);
            }
            return Task.FromResult(CommandStatusResult.Unhandled);
        }

        private Task<CommandStatusResult> getOpenConsoleStatusAsync(string? commandText, CommandStatus progressiveStatus)
        {
            var status = progressiveStatus | CommandStatus.Supported | CommandStatus.Enabled;
            var result = new CommandStatusResult(true, "Open SSH Shell", status);
            return Task.FromResult(result);
        }

        public Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {

            //var profile = this.launchSettingsProvider.ActiveProfile;
            //Assumes.NotNull(profile);
            //var cancellationToken= VsShellUtilities.ShutdownToken;
            //return await this.connectionService.Value
            //    .OpenConsoleAsync(profile, cancellationToken)
            //    .ConfigureAwait(false);
            return TaskResult.True;
        }
    }
}
