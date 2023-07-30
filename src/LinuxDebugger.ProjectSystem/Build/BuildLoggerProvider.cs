using Microsoft.VisualStudio.ProjectSystem.Build;

namespace LinuxDebugger.ProjectSystem.Build
{
    [ExportBuildGlobalPropertiesProvider]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class ProjectPropertiesProvider : StaticGlobalPropertiesProviderBase
    {
        [ImportingConstructor]
        public ProjectPropertiesProvider(ConfiguredProject project)
            : base(project.Services)
        {
            this.Project = project;
        }

        public ConfiguredProject Project { get; }

        public override Task<IImmutableDictionary<string, string>> GetGlobalPropertiesAsync(CancellationToken cancellationToken)
        {
            var dict = new Dictionary<string, string>
            {
                ["RemoteTargetPath"] = "/home/fred"
            };
            IImmutableDictionary<string, string> result = dict.ToImmutableDictionary();
            return Task.FromResult(result);
        }
    }
}
