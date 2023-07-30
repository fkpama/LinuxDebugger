using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Microsoft.VisualStudio.Shell.Interop;
using LinuxDebugger.VisualStudio.Logging;
using System.ComponentModel;
using LinuxDebugger.ProjectSystem.Deployment.Providers;
using LinuxDebugger.ProjectSystem.Deployment;

namespace LinuxDebugger.ProjectSystem.Build
{
    internal interface IProjectDeploymentManager
    {
        ValueTask ExecuteDeploymentHandlersAsync(IDebugSession profile, CancellationToken cancellationToken);
    }

    interface IMeta : IOrderPrecedenceMetadataView
    {
        [DefaultValue(null)]
        string? Name { get; }
    }

    [Export(typeof(IProjectDeploymentManager))]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class ProjectDeploymentManager : IProjectDeploymentManager
    {
        private readonly OrderPrecedenceImportCollection<IRemoteDeploymentProvider, IMeta> providers;
        private readonly IProjectSnapshotService snapshotService;
        private readonly IProjectThreadingService threadingService;
        private readonly IAsyncServiceProvider services;
        private readonly Lazy<ILogger> _log;
        private string? solutionDirectory;

        private ILogger log => _log.Value;
        public ConfiguredProject Project { get; }
        public IBuildProject BuildProject { get; }

        [ImportingConstructor]
        public ProjectDeploymentManager(ConfiguredProject project,
            [ImportMany]
            IEnumerable<Lazy<IRemoteDeploymentProvider, IMeta>> deploymentProviders,
            IBuildProject buildProject,
            IProjectSnapshotService snapshotService,
            IProjectThreadingService threadingService,
            [Import(typeof(SVsServiceProvider))]IAsyncServiceProvider services,
            LoggerService logger) 
        {
            this.Project = project;
            this.BuildProject = buildProject;
            this.snapshotService = snapshotService;
            this.threadingService = threadingService;
            this.services = services;
            this._log = new Lazy<ILogger>(() => logger.GetLogger<ProjectDeploymentManager>());
            var collection = new OrderPrecedenceImportCollection<IRemoteDeploymentProvider, IMeta>(ImportOrderPrecedenceComparer.PreferenceOrder.PreferredComesFirst, project);
            foreach(var item in deploymentProviders)
            {
                collection.Add(item);
            }
            this.providers = collection;
        }

        public async ValueTask ExecuteDeploymentHandlersAsync(IDebugSession profile, CancellationToken cancellationToken)
        {
            var snapshotService = await this.snapshotService
                .GetLatestVersionAsync(this.Project, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var value = snapshotService.Value;
            DeploymentSession? session = null;
            HashSet<string>? lst = null;
            PathMappingProcessor? processor = null;
            string? solutionDirectory = null;
            IOutputGroupsService? outputGroupsService = null;
            foreach(var provider in this.providers)
            {
                lst ??= new HashSet<string>(PathEqualityComparer.Instance);
                solutionDirectory ??= await this
                    .getSolutionDirectoryAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (outputGroupsService is null)
                {
                    outputGroupsService = this.Project.Services.OutputGroups;
                    Assumes.NotNull(outputGroupsService);
                }
                session ??= new DeploymentSession(profile,
                                                  value,
                                                  outputGroupsService,
                                                  this.BuildProject,
                                                  solutionDirectory);
                processor ??= new(session.ProjectDirectory, session.SolutionDirectory);
                var files = await provider.Value
                    .GetAdditionalFileAsync(session, cancellationToken)
                    .ConfigureAwait(false);
                if (files?.Length > 0)
                {
                    log.LogVerbose($"Provided by {provider.Metadata.Name ?? provider.Value.GetType().Name}:\n\t{string.Join("\n\t", files.Select(x => x.Source))}");
                    processor.AddRange(files);
                }
            }

            if (session is not null)
            {
                Debug.Assert(processor is not null);
                if (processor!.HasOutsideFiles)
                {
                    throw new NotImplementedException();
                }

                switch (processor.Root)
                {
                    case PathMappingRoot.Project:
                        var projectDir = session.RemoteProjectDirectory;
                        processor.ProjectDir.RemotePath = projectDir;
                        processor.SolutionDir = null;
                        break;
                    case PathMappingRoot.Solution:
                        Debug.Assert(processor.SolutionDir is not null);
                        projectDir = session.DeploymentDirectory;
                        var relProjectDir = PathUtil.MakeRelative(session.SolutionDirectory, session.ProjectDirectory);
                        var remote = LinuxPath.Combine(projectDir, relProjectDir);
                        session.RemoteProjectDirectory = remote;
                        session.LocalRootDirectory = session.SolutionDirectory;
                        processor.ProjectDir.RemotePath = remote;
                        processor.SolutionDir!.RemotePath = projectDir;
                        break;
                    default:
                        break;
                }

                await this.executeSessionAsync(session, processor, cancellationToken)
                    .ConfigureAwait(false);

                foreach(var provider in this.providers)
                {
                    await provider.Value
                        .ProcessAsync(session, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task executeSessionAsync(DeploymentSession session, PathMappingProcessor processor, CancellationToken cancellationToken)
        {
            List<PathMappingRequest> requests = new();
            if (session.BuildTargetOutputToAdd is not null)
            {
                await getBuildOutputsMappingRequests(requests,
                                                     session.BuildTargetOutputToAdd,
                                                     session.ProjectInstance,
                                                     cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach(var item in processor.ProjectDir.GetFiles())
                session.AddMapping(item);
            if (processor.SolutionDir is not null)
            {
                foreach (var item in processor.SolutionDir.GetFiles())
                    session.AddMapping(item);
            }
        }

        private async Task getBuildOutputsMappingRequests(List<PathMappingRequest> requests,
                                                          IReadOnlyList<string> buildTargetOutputToAdd,
                                                          ProjectInstance projectInstance,
                                                          CancellationToken cancellationToken)
        {
            var designTimeProps = await this.BuildProject
                    .GetDesignTimeBuildPropertiesAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            var mutableProjectInstance = projectInstance.DeepCopy(false);
            var results = await this.BuildProject
                    .BuildAsync(buildTargetOutputToAdd,
                                cancellationToken,
                                projectInstance: mutableProjectInstance,
                                properties: designTimeProps,
                                priority: BuildRequestPriority.Medium)
                    .ConfigureAwait(false);
            var lst = new List<PathMappingRequest>();
            foreach (var result in results.MSBuildResult.ResultsByTarget)
            {
                var items = result.Value.Items.Select(x => x.GetFullPath());
                var dir = Path.GetDirectoryName(this.Project.UnconfiguredProject.FullPath);
                var relPaths = items.Select(x => PathUtil.MakeRelative(dir, x));
                lock (lst)
                {
                    lst.AddRange(relPaths.Select(x => new PathMappingRequest(x, PathMappingRoot.Absolute)));
                }
            }
        }

        private ValueTask<string> getSolutionDirectoryAsync(CancellationToken cancellationToken)
        {
            if (this.solutionDirectory is not null)
                return new(this.solutionDirectory);

            return new(Task.Run(async () =>
            {
                var svc = await this.services
                .GetServiceAsync<SVsSolution, IVsSolution>()
                .ConfigureAwait(false);
                await this.threadingService.SwitchToUIThread();

                ErrorHandler.ThrowOnFailure(
                svc.GetSolutionInfo(out var directory, out _, out _));
                this.solutionDirectory = directory;
                return directory;
            }, cancellationToken));
        }

    }
}
