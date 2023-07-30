using LinuxDebugger.VisualStudio;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using LinuxDebugger.VisualStudio.Settings;

namespace LinuxDebugger.ProjectSystem
{
    struct PropertyHelper
    {
        private IProjectProperties? properties;
        private IProjectSnapshot projectSnapshot;
        private readonly Lazy<IDebugTokenReplacer>[] tokenReplacers;
        private readonly LinuxDebuggerSettings settings;
        private readonly IVsSshClient sshClient;

        public ConfiguredProject Project { get; }
        public string ProjectDir => Path.GetDirectoryName(this.Project.UnconfiguredProject.FullPath);

        public PropertyHelper(ConfiguredProject project,
                               LinuxDebuggerSettings settings,
                               IVsSshClient sshClient)
        {
            var tokenReplacers = project
                .UnconfiguredProject
                .ProjectService
                .Services
                .ExportProvider
                .GetExports<IDebugTokenReplacer>();
            this.Project = project;
            this.settings = settings;
            this.sshClient = sshClient;
            this.properties = null;
            this.tokenReplacers = tokenReplacers?.ToArray() ?? Array.Empty<Lazy<IDebugTokenReplacer>>();
        }

        public Task<string> GetPropertyAsync(string propertyName)
        {
            return GetProperties().GetEvaluatedPropertyValueAsync(propertyName);

        }

        private IProjectProperties GetProperties()
        {
            if (this.properties is null)
            {
                var servicces = this.Project.Services;
                var provider = servicces.ProjectPropertiesProvider;
                Assumes.NotNull(provider);
                this.properties = provider.GetCommonProperties();
            }
            return this.properties;
        }

        internal async Task<string> GetRemoteTargetPathAsync(CancellationToken cancellationToken)
        {
            var outputPath = await this.GetRemoteOutDirAsync(cancellationToken)
                .ConfigureAwait(false);
            var tname = await this.GetTargetNameAsync(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return LinuxPath.Combine(outputPath, tname);
        }

        internal async Task<string> GetTargetNameAsync(CancellationToken cancellationToken)
        {
            var path = await this.GetPropertyAsync("TargetName")
                .ConfigureAwait(false);
            var ext = await this.GetPropertyAsync("TargetExt")
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return $"{path}{ext}";
        }

        internal async Task<string> GetTargetPathAsync(CancellationToken cancellationToken)
        {
            var path = await this.GetPropertyAsync("TargetPath")
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Path.Combine(this.ProjectDir, path);
        }

        internal async Task<string> GetOutputPathAsync(CancellationToken cancellationToken)
        {
            var path = await this
                .GetPropertyAsync("OutputPath")
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return path;
        }

        internal async Task<string> GetFullOutputPathAsync(CancellationToken cancellationToken)
        {
            var path = await this
                .GetOutputPathAsync(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Path.Combine(this.ProjectDir, path);
        }

        internal async Task<string> GetRemoteProjectDirAsync(CancellationToken cancellationToken)
        {
            var projectDir = this.settings.RemoteProjectDirectory;
            var projectName = await this.GetProjectNameAsync(cancellationToken)
                .ConfigureAwait(false);

            projectDir = await this.sshClient
                .ExpandAsync(projectDir, cancellationToken)
                .ConfigureAwait(false);
            return LinuxPath.MakeUnixPath(projectDir, projectName);
        }

        internal async Task<string> GetRemoteOutDirAsync(ILaunchProfile profile, CancellationToken cancellationToken)
        {
            var deploy = profile.GetDeployDirectory();
            if (string.IsNullOrWhiteSpace(deploy))
            {
                deploy = await this.GetRemoteOutDirAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            Assumes.NotNullOrEmpty(deploy);
            return deploy;

        }
        internal async Task<string> GetRemoteOutDirAsync(CancellationToken cancellationToken)
        {
            var projectDir = await this.GetRemoteProjectDirAsync(cancellationToken)
                .ConfigureAwait(false);

            var outputPath = await this.GetOutputPathAsync(cancellationToken)
                .ConfigureAwait(false);

            return LinuxPath.MakeUnixPath(projectDir, outputPath);
        }

        internal async Task<string> GetProjectNameAsync(CancellationToken cancellationToken)
        {
            var path = await this
                .GetPropertyAsync("MSBuildProjectName")
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return path;
        }

        internal async Task<string> ExpandAsync(string value, CancellationToken cancellationToken)
        {
            var @this = this;
            this.projectSnapshot = await getProjectSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var str = this.projectSnapshot.ProjectInstance.ExpandString(value);
            return await @this.sshClient.ExpandAsync(str, cancellationToken).ConfigureAwait(false);
        }

        private ValueTask<IProjectSnapshot> getProjectSnapshotAsync(CancellationToken cancellationToken)
        {

            if (this.projectSnapshot is not null)
            {
                return new(this.projectSnapshot);
            }
            return new(doGetSnapshotAsync(cancellationToken));
        }
        async Task<IProjectSnapshot> doGetSnapshotAsync(CancellationToken cancellationToken)
        {
            var snapshotService = this.Project.Services.ProjectSnapshotService;
            Assumes.NotNull(snapshotService);
            var version = await snapshotService
                .GetLatestVersionAsync(this.Project, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            this.projectSnapshot = version.Value;
            return this.projectSnapshot;
        }

        internal async Task DeployAsync(ILaunchProfile profile, CancellationToken cancellationToken)
        {

            var outDir = await this
                .GetRemoteOutDirAsync(profile, cancellationToken)
                .ConfigureAwait(false);

            var localDir = await this
                .GetFullOutputPathAsync(cancellationToken)
                .ConfigureAwait(false);

            await this.sshClient.UploadAsync(localDir, outDir, cancellationToken).ConfigureAwait(false);
        }
    }
}