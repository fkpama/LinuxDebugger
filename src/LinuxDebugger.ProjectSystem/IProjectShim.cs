using System.Diagnostics;
using LinuxDebugger.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using LinuxDebugger.VisualStudio.Logging;
using LinuxDebugger.VisualStudio.Settings;
using LinuxDebugger.ProjectSystem.Deployment.Providers;
using LinuxDebugger.ProjectSystem.Build;

namespace LinuxDebugger.ProjectSystem
{
    public interface IDebugSession
    {
        IProjectSnapshot Project { get; }
        DirectoryBaseHandle RemoteOutputDirectory { get; set; }
        DirectoryBaseHandle RemoteProjectDirectory { get; set; }
        void AddMapping(SimplePathMapping mapping);
    }
    internal sealed class DebugSessionInfo : IDebugSession
    {
        private readonly IProjectSnapshot projectSnapshot;

        private List<SimplePathMapping>? mappings;

        public IProjectShim Project { get; }
        IProjectSnapshot IDebugSession.Project => this.projectSnapshot;
        public DebugTargetInfo TargetInfo { get; internal set; }
        /// <summary>
        /// Working directory on the remote
        /// </summary>
        public DirectoryBaseHandle WorkingDir { get; }
        public DirectoryBaseHandle RemoteOutputDirectory { get; set; }
        public string LocalOutDir { get; }
        public DirectoryBaseHandle RemoteProjectDirectory { get; set; }
        //string IDebugSession.RemoteProjectDirectory
        //{
        //    get => this.RemoteProjectDirectory;
        //    set => this.RemoteProjectDirectory = new DirectoryHandle(value);
        //}

        public IVsSshClient Client => this.TargetInfo.SshClient;

        internal DebugSessionInfo(IProjectShim projectShim,
                                  IProjectSnapshot projectSnapshot,
                                  DirectoryBaseHandle workingDir,
                                  string targetPath,
                                  string localOutDir,
                                  DirectoryBaseHandle remoteProjectDir)
        {
            this.Project = projectShim;
            this.projectSnapshot = projectSnapshot;
            this.WorkingDir = workingDir;
            this.RemoteOutputDirectory = targetPath;
            this.LocalOutDir = localOutDir;
            this.RemoteProjectDirectory = remoteProjectDir;
        }

        public async ValueTask EnsureVsDbgAsync(CancellationToken cancellationToken)
        {
            await this.Project
                .EnsureVsDbgAsync(this.Client, cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask DeployAsync(CancellationToken cancellationToken)
        {
            //await this.Client.UploadAsync(this.LocalOutDir,
            //                              this.RemoteOutputDirectory,
            //                              cancellationToken)
            //    .ConfigureAwait(false);

            //var lst = new List<Task>();
            if (this.mappings is not null)
            {
                await this.Client
                    .UploadAsync(this.mappings.ToArray(), cancellationToken)
                    .ConfigureAwait(false);
            }
            //await Task.WhenAll(lst).ConfigureAwait(false);
        }

        internal Task ReleaseAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
            //throw new NotImplementedException();
        }

        public void AddMapping(SimplePathMapping mapping)
        {
            (this.mappings ??= new()).Add(mapping);
        }
    }

    internal interface IProjectShim
    {
        ConfiguredProject Project { get; }

        ValueTask EnsureVsDbgAsync(CancellationToken cancellationToken);
        ValueTask EnsureVsDbgAsync(IVsSshClient sshClient, CancellationToken cancellationToken);
        IReadOnlyDictionary<string, string> GetDebugEnvironmentVariables();
        ValueTask<IDebugLaunchSettings> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions,
            TargetLaunchOption targetLaunchOption,
            IReadOnlyDictionary<string, string>? args,
            CancellationToken cancellationToken);
        ValueTask<DebugSessionInfo> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions,
            TargetLaunchOption targetLaunchOption,
            ILaunchProfile profile,
            IVsSshClient? client,
            CancellationToken cancellationToken);
    }

    [Export(typeof(IProjectShim))]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ProjectShim : IProjectShim
    {
        private readonly ILinuxDebuggerSettingsManager settingsManager;
        private readonly ISshConnectionService connectionService;
        private readonly IProjectDeploymentManager deploymentManager;
        private readonly OrderPrecedenceImportCollection<IVsHierarchy> vsHierarchies;
        private string? vsDbgBasePath;
        private readonly AsyncLazy<LinuxDebuggerSettings> settingsLazy;
        private readonly AsyncLazy<ILogger> logLazy;

        internal VsConnectionManager ConnectionManager => this.connectionService.Manager;
        public ConfiguredProject Project { get; }

        private ILogger log => this.logLazy.GetValue(VsShellUtilities.ShutdownToken);

        public IVsHierarchy? VsHierarchy => this.vsHierarchies.FirstOrDefault()?.Value;

        public string VsDbgBasePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.vsDbgBasePath))
                {
                    var path = settingsLazy.GetValue().VsDbgDirectory;
                    path = string.IsNullOrWhiteSpace(path)
                        ? LinuxConstants.DefaultVsdbgBasePath
                        : path;
                    Assumes.NotNull(path);
                    this.vsDbgBasePath = LinuxPath.Combine(path, LinuxConstants.VS2022);
                }
                Assumes.NotNull(vsDbgBasePath);
                return vsDbgBasePath;
            }
        }

        public string VsDbgPath
        {
            get => LinuxPath.Combine(this.VsDbgBasePath, LinuxConstants.AppVSDbg);
        }

        [ImportingConstructor]
        public ProjectShim(ConfiguredProject project,
            ILinuxDebuggerSettingsManager settingsManager,
            [Import(ExportContractNames.Scopes.ProjectService)]ISshConnectionService connectionService,
            IProjectThreadingService threadingService,
            [ImportMany(ExportContractNames.VsTypes.IVsHierarchy)]
            IEnumerable<Lazy<IVsHierarchy, IOrderPrecedenceMetadataView>> hierarchies,
            IProjectDeploymentManager deploymentManager,
            LoggerService log)
        {
            this.Project = project;
            this.logLazy = new(() => log.GetLoggerAsync<ProjectShim>(VsShellUtilities.ShutdownToken).AsTask(),
                threadingService.JoinableTaskFactory);
            this.settingsManager = settingsManager;
            this.connectionService = connectionService;
            this.deploymentManager = deploymentManager;
            this.vsHierarchies = hierarchies.ToImportCollection(project);
            this.settingsLazy = new(() =>
            {
                return this.settingsManager
                .GetSettingsAsync(VsShellUtilities.ShutdownToken)
                .AsTask();
            }, ThreadHelper.JoinableTaskFactory);
        }

        public async ValueTask EnsureVsDbgAsync( CancellationToken cancellationToken)
        {
            var client = await this.ConnectionManager
                .GetDefaultConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            await EnsureVsDbgAsync(client, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask EnsureVsDbgAsync(IVsSshClient sshClient, CancellationToken cancellationToken)
        {
            await this.ConnectionManager.GetDefaultConnectionAsync(cancellationToken).ConfigureAwait(false);
            var path = this.VsDbgBasePath;
            path = sshClient.Expand(path);

            if (!await sshClient
                .FileExistsAsync(this.VsDbgPath, cancellationToken)
                .ConfigureAwait(false))
            {
                bool isWget = false;
                var exe = await sshClient
                    .ExecutableExistsAsync("curl", cancellationToken)
                    .ConfigureAwait(false);
                if (!exe)
                {
                    isWget = await sshClient.ExecutableExistsAsync("wget", cancellationToken)
                        .ConfigureAwait(false);
                    if (exe)
                    {
                    }
                    else
                    {
                        await sshClient
                            .RunCommandAsync("apt-get -y install curl", cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (!exe)
                {
                    throw new NotImplementedException();
                }

                string cmdText;
                path = LinuxPath.EscapeForUnixShell(path);
                const string address = "https://aka.ms/getvsdbgsh";
                if (!isWget)
                {
                    cmdText = $"bash /dev/stdin -v latest -l {path} <<EOF\n$(curl -ksSL {address})\nEOF";
                }
                else
                {
                    cmdText = $"wget {address} -P {path}";
                }

                await sshClient
                    .RunCommandAsync(cmdText, TimeSpan.FromMinutes(2), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask<DebugSessionInfo> QueryDebugTargetsAsync(
            DebugLaunchOptions options,
            TargetLaunchOption targetLaunchOption,
            ILaunchProfile profile,
            IVsSshClient? client,
            CancellationToken cancellationToken)
        {

            await TaskScheduler.Default;

            var dbgConfig = await this.settingsManager
                .GetSettingsAsync(cancellationToken)
                .ConfigureAwait(false);

            var indicator = createProgressIndicator(cancellationToken);
            await indicator.ShowProgressAsync(cancellationToken).ConfigureAwait(false);

            var redirectionDisabled = profile.GetRedirectionsDisabled();
            try
            {
                var tproject = getCurrentSnapshotAsync(cancellationToken);
                cancellationToken = indicator.CancellationToken;
                //if (client is not null)
                //{
                //    await client
                //        .ConnectAsync(cancellationToken)
                //        .ConfigureAwait(false);
                //}
                client ??= await getClientAsync(profile, cancellationToken)
                        .ConfigureAwait(false);

                var helper = new PropertyHelper(this.Project,
                                             dbgConfig,
                                             client);
                DirectoryBaseHandle? exePath = profile.ExecutablePath!;
                DirectoryBaseHandle workingDir;
                string? deploymentDir;
                IReadOnlyDictionary<string, string>? envVars;
                string? args = profile.CommandLineArgs;

                envVars = profile.EnvironmentVariables;
                var projectName = await helper.GetProjectNameAsync(cancellationToken)
                    .ConfigureAwait(false);

                var remoteProjectDir = new DirectoryHandle(LinuxPath
                    .Combine(dbgConfig.RemoteProjectDirectory, projectName));

                remoteProjectDir = await client
                    .ExpandAsync(remoteProjectDir, cancellationToken)
                    .ConfigureAwait(false);

                var outputPath = await helper
                    .GetOutputPathAsync(cancellationToken)
                    .ConfigureAwait(false);


                //targetLaunchOption |= TargetLaunchOption.Deploy;
                deploymentDir = profile.GetDeployDirectory();
                if (deploymentDir.IsMissing())
                {
                    deploymentDir = await helper.GetRemoteOutDirAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    deploymentDir = await helper.ExpandAsync(deploymentDir!, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (exePath is null)
                {
                    var targetName = await helper
                        .GetTargetNameAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var remoteOutputPath = LinuxPath.ConvertWindowsPathToUnixPath(outputPath);
                    exePath = new PathCombination(remoteProjectDir, remoteOutputPath, targetName);
                    //exePath = LinuxPath.Combine(deploymentDir, targetName);
                }
                else
                {
                    Assumes.NotNullOrEmpty(exePath);
                    exePath = await helper
                        .ExpandAsync(exePath, cancellationToken)
                        .ConfigureAwait(false);
                    if (!LinuxPath.IsRooted(exePath))
                    {
                        exePath = LinuxPath.MakeUnixPath(remoteProjectDir, exePath);
                    }

                }

                if (profile.WorkingDirectory.IsMissing())
                {
                    if (Project.Capabilities.Contains(Constants.Capabilities.AspNetCore))
                    {
                        workingDir = new DirectoryReference(remoteProjectDir);
                    }
                    else if (deploymentDir.IsPresent())
                    {
                        Assumes.NotNull(deploymentDir);
                        workingDir = deploymentDir;
                    }
                    else
                    {
                        workingDir = await helper
                            .GetRemoteOutDirAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    workingDir = profile.WorkingDirectory!;
                }
                Assumes.NotNull(workingDir);
                if (workingDir is not DirectoryReference
                    && !LinuxPath.IsRooted(workingDir))
                {
                    workingDir = LinuxPath.MakeUnixPath(remoteProjectDir, workingDir);
                }

                //if (targetLaunchOption.HasFlag(TargetLaunchOption.InstallVsDbgShim))
                //{
                //    indicator.UpdateProgress("Installing VsDbg");
                //    await this.EnsureVsDbgAsync(client, cancellationToken).ConfigureAwait(false);
                //}

                var fullOutputPath = await helper
                    .GetFullOutputPathAsync(cancellationToken)
                    .ConfigureAwait(false);

                var launcher = new DebugLauncher(client, dbgConfig, log)
                {
                    VsDbgBasePath = this.VsDbgBasePath
                };

                string? tty = null;
                if (!redirectionDisabled)
                {
                    indicator.UpdateProgress("Opening remote TTY");
                    tty = await this.connectionService
                        .OpenTtyAsync(profile, client, cancellationToken)
                        .ConfigureAwait(false);
                }

                var project = await tproject.ConfigureAwait(false);
                var session = new DebugSessionInfo(this,
                                                   project,
                                                   workingDir,
                                                   deploymentDir,
                                                   fullOutputPath,
                                                   remoteProjectDir);

                indicator.UpdateProgress("Executing deployment handlers");
                await this.deploymentManager
                    .ExecuteDeploymentHandlersAsync(session, cancellationToken)
                    .ConfigureAwait(false);
                session.TargetInfo = await launcher
                    .QueryDebugTargetsAsync(options,
                                            exePath,
                                            args,
                                            session.WorkingDir,
                                            tty,
                                            envVars,
                                            cancellationToken)
                .ConfigureAwait(false);
                return session;
            }
            finally
            {
                indicator.CloseProgress(null);
            }
        }

        private async Task<IProjectSnapshot> getCurrentSnapshotAsync(CancellationToken cancellationToken)
        {
            var svc = this.Project.Services.ProjectSnapshotService;
            Assumes.NotNull(svc);
            var project = await svc.GetLatestVersionAsync(this.Project, cancellationToken: cancellationToken)
                                .ConfigureAwait(false);
            return project.Value;
        }

        private async Task<IVsSshClient> getClientAsync(ILaunchProfile profile, CancellationToken cancellationToken)
        {
            return await this.connectionService
                .GetConnectionAsync(profile, force: true, cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask<IDebugLaunchSettings> QueryDebugTargetsAsync(
            DebugLaunchOptions options,
            TargetLaunchOption targetLaunchOption,
            IReadOnlyDictionary<string, string>? parameters,
            CancellationToken cancellationToken)
        {
            var indicator = createProgressIndicator(cancellationToken);

            _ = Task.Run(async () =>
            {
                await TaskScheduler.Default;
                try
                {
                    await indicator
                    .ShowProgressAsync(cancellationToken)
                    .ConfigureAwait(false);
                    var result = await doQueryDebugTargetAsync(indicator,
                                                  options,
                                                  targetLaunchOption,
                                                  cancellationToken)
                    .ConfigureAwait(false);
                    indicator.CloseProgress(result.Settings);
                }
                catch (Exception ex)
                {
                    indicator.SetException(ex);
                }
            });

            return new(indicator.ProgressTask);
        }

        private ProgressIndicator<IDebugLaunchSettings> createProgressIndicator(CancellationToken cancellationToken)
        {
            var indicator = new ProgressIndicator<IDebugLaunchSettings>(
                "Remote Linux",
                "Connecting",
                cancellationToken);
            return indicator;
        }

        private async ValueTask<DebugTargetInfo> doQueryDebugTargetAsync(
            IProgressIndicator indicator,
            DebugLaunchOptions options,
            TargetLaunchOption targetLaunchOption,
            CancellationToken cancellationToken)
        {
            await TaskScheduler.Default;

            var dbgConfig = await this.settingsManager
                .GetSettingsAsync(cancellationToken)
                .ConfigureAwait(false);

            var hier = this.VsHierarchy;
            ILogger log;
            Debug.Assert(hier is not null);
            cancellationToken = indicator.CancellationToken;
            indicator.UpdateProgress("Connecting to system");
            var client = await this.ConnectionManager
                .GetDefaultConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            var helper = new PropertyHelper(this.Project,
                dbgConfig,
                client);

            log = await this.logLazy
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false);

            string? args = null;
            var fullOutputPath = await helper
                    .GetFullOutputPathAsync(cancellationToken)
                    .ConfigureAwait(false);
            var remoteOutDir = await helper
                .GetRemoteOutDirAsync(cancellationToken)
                .ConfigureAwait(false);

            if (targetLaunchOption.HasFlag(TargetLaunchOption.InstallVsDbgShim))
            {
                indicator.UpdateProgress("Installing VsDbg");
                await this.EnsureVsDbgAsync(client, cancellationToken).ConfigureAwait(false);
            }

            if (targetLaunchOption.HasFlag(TargetLaunchOption.Deploy))
            {
                indicator.UpdateProgress("Deploying binaries");
                await this.doDeployAsync(client,
                                         fullOutputPath,
                                         remoteOutDir,
                                         cancellationToken)
                    .ConfigureAwait(false);
            }

            //var fullOutputPath = Path.Combine(projectDir, outputPath);
            var launcher = new DebugLauncher(client, dbgConfig, log)
            {
                VsDbgBasePath = this.VsDbgBasePath,
            };


            //var dllName = $"{targetName}.dll";
            var dllName = await helper
                .GetTargetNameAsync(cancellationToken)
                .ConfigureAwait(false);
            var targets = await launcher
                .QueryDebugTargetsAsync(options,
                                        dllName,
                                        args,
                                        remoteOutDir,
                                        null,
                                        null,
                                        cancellationToken)
                .ConfigureAwait(true);
            targets.Settings.Project = this.VsHierarchy;

            return targets;

            //static string getFallbackTargetPath(string config, string tfm)
            //    => Path.Combine("bin", config ?? string.Empty, tfm ?? string.Empty);

        }

        public async Task doDeployAsync(IVsSshClient client,
            string sourceDir,
                                      string remoteDirectory,
                                      CancellationToken cancellationToken)
        {
            await client.UploadAsync(sourceDir, remoteDirectory, cancellationToken).ConfigureAwait(false);
        }

        public IReadOnlyDictionary<string, string> GetDebugEnvironmentVariables()
        {
            if (this.Project.Capabilities.Contains(Constants.Capabilities.DotNetCoreWeb))
            {
                return new Dictionary<string, string>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development"
                };
            }
            return ImmutableDictionary<string, string>.Empty;
        }
    }
}
