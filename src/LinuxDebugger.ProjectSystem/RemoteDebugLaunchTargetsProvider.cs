using System.Runtime.CompilerServices;
using System.Xml;
using LinuxDebugger.ProjectSystem.Build;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell.Interop;

namespace LinuxDebugger.ProjectSystem
{
    [Export(typeof(IDebugProfileLaunchTargetsProvider))]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed partial class RemoteDebugLaunchTargetsProvider
        : IDebugProfileLaunchTargetsProvider
    {
        readonly struct CommandExecInfo
        {
            public readonly bool IgnoreExitCode;
            public readonly string CommandText;
            public CommandExecInfo(string commandText, bool ignoreExitCode = false)
            {
                this.CommandText = commandText;
                this.IgnoreExitCode = ignoreExitCode;
            }
        }

        private readonly DebuggerEvents currentEvents;
        private readonly IProjectShim projectShim;
        private readonly IProjectThreadingService threadingService;
        private readonly IProjectDeploymentManager deploymentManager;
        private readonly ConditionalWeakTable<string, IVsSshClient> sshClients = new();
        private readonly ConditionalWeakTable<ILaunchProfile, DebugSessionInfo> infos = new();

        [ImportingConstructor]
        public RemoteDebugLaunchTargetsProvider(
            IProjectShim projectShim,
            IAdditionalRuleDefinitionsService additionalRules,
            [ImportMany(ExportContractNames.VsTypes.VSProject)]
            IEnumerable<Lazy<IVsProject, IOrderPrecedenceMetadataView>> vsProjects,
            IProjectThreadingService threadingService,
            IProjectDeploymentManager deploymentManager,
            [Import(typeof(SVsServiceProvider))] IAsyncServiceProvider sp)
        {
            this.projectShim = projectShim;
            this.threadingService = threadingService;
            this.deploymentManager = deploymentManager;
            this.currentEvents = new(sp, threadingService);
        }

        public Task OnAfterLaunchAsync(DebugLaunchOptions launchOptions,
                                       ILaunchProfile profile)
            => Task.CompletedTask;

        public async Task OnBeforeLaunchAsync(DebugLaunchOptions launchOptions,
                                              ILaunchProfile profile)
        {
            var cancellationToken = VsShellUtilities.ShutdownToken;
            if (!this.infos.TryGetValue(profile, out var session))
            {
                throw new NotImplementedException();
            }
            this.infos.Remove(profile);

            try
            {
                await onBeforeLaunchAsync(session,
                                          profile,
                                          cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                await this.currentEvents.ResetAsync(cancellationToken).ConfigureAwait(false);
                await session.ReleaseAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task onBeforeLaunchAsync(DebugSessionInfo session,
                                               ILaunchProfile profile,
                                               CancellationToken cancellationToken)
        {

            using var indicator = new ProgressIndicator<int>(
                "Remote Linux",
                "Installing vsDbg",
                cancellationToken);
            await session
                .EnsureVsDbgAsync(cancellationToken)
                .ConfigureAwait(false);

            await this.executeEventsAsync(profile, cancellationToken)
                .ConfigureAwait(false);

            //await this.deploymentManager
            //    .ExecuteDeploymentHandlersAsync(session, cancellationToken)
            //    .ConfigureAwait(false);

            indicator.UpdateProgress("Deploying files");
            await session.DeployAsync(cancellationToken)
                .ConfigureAwait(false);

            indicator.UpdateProgress("Executing pre-launch commands");
            await this.executeCommandsAsync(profile, Constants.ProfileParams.CommandPre)
                .ConfigureAwait(false);
        }

        private async Task executeEventsAsync(ILaunchProfile profile, CancellationToken cancellationToken)
        {
            if (this.currentEvents is not null)
            {
                if (this.currentEvents.CanExecute(profile))
                {
                    try
                    {
                        await this.currentEvents
                            .DoUploadsAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        await this.currentEvents
                            .ResetAsync(cancellationToken)
                            .ConfigureAwait(false);
                        throw;
                    }
                }
            }
        }

        private async Task executeCommandsAsync(ILaunchProfile profile, string commandPre)
        {
            Assumes.NotNullOrEmpty(profile?.Name);
            CancellationToken cancellationToken = VsShellUtilities.ShutdownToken;
            var lst = new Dictionary<string, CommandExecInfo>();
            IVsSshClient? sshClient = null;
            if (profile.OtherSettings?.TryGetValue(commandPre, out var cmdObject) == true)
            {
                if(!this.sshClients.TryGetValue(profile.Name, out sshClient))
                {
                    throw new NotImplementedException();
                }

                if (cmdObject is string singleCmd)
                {
                    lst.Add(string.Empty, getCommandInfo(singleCmd));
                }
                else if (cmdObject is IReadOnlyDictionary<string, object> dict)
                {
                    foreach(var kvp in dict)
                    {
                        var name = kvp.Key;
                        var value = getCommandInfo(kvp.Value);
                        lst.Add(name, value);
                    }
                }
                else
                {
                }
            }

            if (lst?.Count > 0)
            {
                Assumes.NotNull(sshClient);
                foreach (var kvp in lst)
                {
                    var name = kvp.Key;
                    var value = kvp.Value;
                    if (value.CommandText.IsMissing())
                        continue;

                    try
                    {
                        await sshClient
                            .RunCommandAsync(value.CommandText, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch(ExitCodeException ex)
                    when (value.IgnoreExitCode)
                    {
                    }
                }
            }
        }

        private CommandExecInfo getCommandInfo(object value)
        {
            if (value is string str)
                return new(str);

            if (value is IReadOnlyDictionary<string, object> dict)
            {
                if (dict.TryGetValue("commandText", out var s)
                    && s is string s2)
                {
                    bool ignoreExitCode = false;
                    if (dict.TryGetValue("ignoreExitCode", out s))
                    {
                        if (s is string s3)
                        {
                            try
                            {
                                ignoreExitCode = XmlConvert.ToBoolean(s3);
                            }
                            catch (FormatException ex)
                            {
                                // TODO: Log
                            }
                        }
                    }
                    return new(s2, ignoreExitCode);
                }
                else
                {
                    // TODO: log
                    return default;
                }

            }

            // TODO: log
            return default;
        }

        public async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions,
            ILaunchProfile profile)
        {
            var cancellationToken = VsShellUtilities.ShutdownToken;
            Assumes.NotNullOrEmpty(profile.Name);
            var options = TargetLaunchOption.None;
            var hadClient =  this.sshClients.TryGetValue(profile.Name, out var client);

            var downloads = profile.GetDownloadPathMappings().ToArray();
            var uploads = profile.GetUploadPathMappings().ToArray();

            var session = await this.projectShim
                .QueryDebugTargetsAsync(launchOptions,
                                        options,
                                        profile,
                                        client,
                                        cancellationToken)
                .ConfigureAwait(false);
            var item = session.TargetInfo;
            Assumes.NotNull(item.SshClient);
            if (!hadClient)
                this.sshClients.Add(profile.Name, item.SshClient);

            if (downloads.Length > 0)
                Utils.ComputeDownloads(downloads, session);

            if (uploads.Length > 0)
                Utils.ComputeUploads(uploads, session.Client, session);

            await this.currentEvents.SetupAsync(item.SshClient,
                                                downloads,
                                                uploads,
                                                profile,
                                                cancellationToken)
                .ConfigureAwait(false);

            if (this.infos.TryGetValue(profile, out var prev))
            {
                // ??
                await prev.ReleaseAsync(VsShellUtilities.ShutdownToken)
                    .ConfigureAwait(false);
                this.infos.Remove(profile);
            }
            this.infos.Add(profile, session);
            return new[] { item.Settings };
        }

        public bool SupportsProfile(ILaunchProfile profile)
            => string.Equals(profile.CommandName, Constants.CommandName);
    }
}
