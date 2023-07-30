using System.Diagnostics;
using System.Text;
using LinuxDebugger.ProjectSystem.Deployment.Providers;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Microsoft.VisualStudio.Settings.Internal;
using Microsoft.VisualStudio.Utilities;

namespace LinuxDebugger.ProjectSystem.Build
{
    internal sealed class DeploymentSession : IDeploymentSession
    {
        sealed class BuildContext
        {
            internal ProjectInstance ProjectInstance;
            internal ConfigurableForwardingLogger Logger;
            internal StringLogger Redirector;
            internal IImmutableDictionary<string, string> Properties;
            private IImmutableSet<ILogger>? loggers;

            internal IImmutableSet<ILogger> Loggers
            {
                get => this.loggers ??= new ILogger[] { this.Logger }.ToImmutableHashSet();
            }

            internal string GetErrors()
            {
                var sb = PooledStringBuilder.GetInstance();
                try
                {
                    foreach (var item in this.Redirector.Events.OfType<BuildErrorEventArgs>())
                    {
                        sb.Builder.AppendLine($"{item.Code}: {item.Message}");
                    }
                    return sb.ToStringAndFree();
                }
                catch
                {
                    sb.Free();
                    throw;
                }
            }
        }
        sealed class StringLogger : IEventRedirector
        {
            List<BuildEventArgs>? args;
            public IReadOnlyList<BuildEventArgs> Events
            {
                get => (IReadOnlyList<BuildEventArgs>?)this.args ?? Array.Empty<BuildEventArgs>();
            }
            public StringLogger()
            {
            }
            public void ForwardEvent(BuildEventArgs buildEvent)
            {
                (this.args ??= new()).Add(buildEvent);
            }
        }
        private readonly IDebugSession profile;
        private readonly IBuildProject builderProject;
        private List<string>? buildTargetOutputs;
        private string? outDir;
        private Dictionary<object, object>? properties;
        private BuildContext? projectInstance;

        public IDictionary<object, object> Properties
        {
            get
            {
                if (properties is null)
                {
                    this.properties = new();
                }
                return this.properties;
            }
        }

        public string RemoteProjectDirectory
        {
            get => this.profile.RemoteProjectDirectory;
            set => this.profile.RemoteProjectDirectory.SetPath(value);
        }
        public string DeploymentDirectory { get; internal set; }
        public string ProjectDirectory { get; }
        public string LocalOutputDirectory
        {
            get
            {
                if (this.outDir is null)
                {
                    var odir = this.ProjectInstance.GetPropertyValue("OutDir");
                    var dir = Path.GetDirectoryName(this.ProjectInstance.FullPath);
                    this.outDir = Path.Combine(dir, odir);
                }
                return this.outDir;
            }
        }

        internal IReadOnlyList<string>? BuildTargetOutputToAdd
        {
            get => this.buildTargetOutputs;
        }

        public IProjectSnapshot Project { get; }
        public IOutputGroupsService OutputGroups { get; }
        public string SolutionDirectory { get; }
        public DeploymentType DeploymentType { get; }
        internal ProjectInstance ProjectInstance
        {
            get => this.Project.ProjectInstance;
        }
        public string LocalRootDirectory { get; internal set; }

        internal DeploymentSession(IDebugSession profile,
                                   IProjectSnapshot project,
                                   IOutputGroupsService outputGroups,
                                   IBuildProject builderProject,
                                   string solutionDirectory)
        {
            this.DeploymentType = DeploymentType.Build;
            this.profile = profile;
            this.Project = project;
            this.OutputGroups = outputGroups;
            this.builderProject = builderProject;
            var projDir = project
                .ProjectInstance
                .GetPropertyValue(Constants.MSBuild.ProjectProperties.MSBuildProjectDirectory);
            this.ProjectDirectory = projDir;
            this.SolutionDirectory = solutionDirectory;
            this.DeploymentDirectory = profile.RemoteProjectDirectory;
            this.LocalRootDirectory = projDir;
        }

        public ValueTask<ITaskItem[]> GetCopyToOutputDirectoryItemsAsync(CancellationToken cancellationToken)
            => this.GetBuildTargetOutputsAsync(
                new[] { Constants.MSBuild.GetCopyToOutputDirectoryItemsTargetName },
                cancellationToken);
        public ValueTask<ITaskItem[]> GetBuildTargetOutputsAsync(IEnumerable<string> targets, CancellationToken cancellationToken)
        {
            var filtered = targets;
            return new(Task.Run(async () =>
            {
                if (this.projectInstance is null)
                {
                    var properties = await this.builderProject
                .GetDesignTimeBuildPropertiesAsync(cancellationToken)
                .ConfigureAwait(false);

                    var project = this.ProjectInstance.DeepCopy(false);

                    //foreach (var prop in properties)
                    //{
                    //    project.SetProperty(prop.Key, prop.Value);
                    //}
                    var log1 = new ConfigurableForwardingLogger();
                    var redirect = new StringLogger();
                    log1.BuildEventRedirector = redirect;
                    this.projectInstance = new()
                    {
                        ProjectInstance = project,
                        Properties = properties,
                        Logger= log1,
                        Redirector = redirect
                    };
                }

                //if (!this.projectInstance
                //.ProjectInstance
                //.Build(filtered.ToArray(), this.projectInstance.Loggers, out var outputs))
                //{
                //    var msg = this.projectInstance.GetErrors();
                //    throw new BuildAbortedException(msg);
                //}
                //return outputs.Values.SelectMany(x => x.Items).ToArray();
                var result = await this.builderProject
                .BuildAsync(filtered,
                            cancellationToken,
                            projectInstance: this.projectInstance.ProjectInstance,
                            loggers: this.projectInstance.Loggers,
                            properties: this.projectInstance.Properties,
                            priority: BuildRequestPriority.Medium)
                .ConfigureAwait(false);

                if (result.OverallResult == BuildResultCode.Failure)
                {
                    if (result.MSBuildResult.Exception is not null)
                    {
                        result.MSBuildResult.Exception.Rethrow();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                return result.MSBuildResult.ResultsByTarget
                .SelectMany(x => x.Value.Items)
                .ToArray();

            }, cancellationToken));
        }

        public ValueTask<T> GetPropertyAsync<T>(string property, CancellationToken cancellationToken)
        {
            var value = this.Project.ProjectInstance.GetPropertyValue(property);
            return new((T)Convert.ChangeType(value, typeof(T)));
        }

        public ValueTask<string> GetPropertyAsync(string property, CancellationToken cancellationToken)
        {
            var value = this.Project.ProjectInstance.GetPropertyValue(property);
            return new(value);
        }

        public void AddBuildTargetOutputs(string targetName)
        {
            (this.buildTargetOutputs ??= new()).Add(targetName);
        }

        public void AddMapping(SimplePathMapping mapping)
        {
            this.profile.AddMapping(mapping);
        }

        public async ValueTask<SimplePathMapping[]> GetOutputGroupItemsAsync(string name, CancellationToken cancellationToken)
        {
            var outputGroup = await this.OutputGroups
                .GetOutputGroupAsync(name, cancellationToken)
                .ConfigureAwait(false);
            return outputGroup
                .Outputs
                .Select(x =>
                {
                    var src = x.Value["FullPath"];
                    var targetPath = Path.Combine(this.LocalOutputDirectory, x.Value["TargetPath"]);
                    return new SimplePathMapping(src, targetPath);
                }).ToArray();
        }
    }
}