using Microsoft.Build.Framework.XamlTypes;
//using Microsoft.VisualStudio.Linux.ConnectionManager;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem
{
    [ExportDebugger("My")]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    // TODO: for IDebugQueryTarget: https://github.com/dotnet/project-system/issues/3835#issuecomment-412619049
    internal sealed class LaunchProvider : DebugLaunchProviderBase, IDebugQueryTarget
    {
        private readonly IProjectShim projectShim;
        private readonly ILaunchSettingsProvider settingsProvider;

        [ImportingConstructor]
        public LaunchProvider(ConfiguredProject configuredProject,
                              IProjectShim projectShim,
                              IAdditionalRuleDefinitionsService additionalRules,
                              ILaunchSettingsProvider settingsProvider)
            : base(projectShim.Project)
        {
            this.projectShim = projectShim;
            this.settingsProvider = settingsProvider;
            if (!additionalRules.AddRuleDefinition(getRule(), PropertyPageContexts.Project))
            {
            }
            if (!additionalRules.AddRuleDefinition(Utils.GetRuleFilePath("Rule.xml"), PropertyPageContexts.Project))
            {
            }
        }

        private static Rule getRule()
        {
            var rule = new Rule();
            rule.BeginInit();
            rule.Name = "My";
            rule.DisplayName = "Remote Linux";
            rule.PageTemplate = "debugger";
            rule.Description = "Linux Remote debugger options";

            rule.DataSource = new()
            {
                Persistence = "UserFile",
                SourceOfDefaultValue = DefaultValueSourceLocation.AfterContext
            };

            rule.EndInit();

            return rule;
        }

        public override async Task<bool> CanLaunchAsync(DebugLaunchOptions launchOptions)
        {
            if (!await this.ConfiguredProject
                .HasRemoteLinuxCapabilityAsync(VsShellUtilities.ShutdownToken)
                .ConfigureAwait(false))
            {
                return false;
            }
            return true;
        }

        public override  async Task LaunchAsync(DebugLaunchOptions launchOptions)
        {
            var cancellationToken = VsShellUtilities.ShutdownToken;
            try
            {
                await TaskScheduler.Default;
                var options = TargetLaunchOption.InstallVsDbgShim
                    | TargetLaunchOption.Deploy;
                var targets = await this.projectShim
                .QueryDebugTargetsAsync(launchOptions,
                                        options,
                                         null,
                                        cancellationToken)
                .ConfigureAwait(false);

                var ret = new[]{ targets };
                await this.ThreadingService.SwitchToUIThread();
                await base.LaunchAsync(ret).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        public override async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions)
        {
            var cancellationToken = VsShellUtilities.ShutdownToken;
            var options = TargetLaunchOption.None;
            var targets = await this.projectShim
                .QueryDebugTargetsAsync(launchOptions,
                                        options,
                                         null,
                                        cancellationToken)
                .ConfigureAwait(false);

            var ret = new[]{ targets };
            return ret;
        }
    }
}