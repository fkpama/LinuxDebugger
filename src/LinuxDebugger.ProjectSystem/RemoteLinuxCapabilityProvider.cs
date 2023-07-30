
namespace LinuxDebugger.ProjectSystem
{
    [Export(ExportContractNames.Scopes.ConfiguredProject, typeof(IProjectCapabilitiesProvider))]
    [AppliesTo(ProjectCapabilities.Cps)]
    internal sealed class RemoteLinuxCapabilityProvider : ProjectCapabilitiesProviderBase
    {
        public ConfiguredProject Project { get; }
        [ImportingConstructor]

        public RemoteLinuxCapabilityProvider(ConfiguredProject project)
            : base(nameof(RemoteLinuxCapabilityProvider),
                  project.Services.ThreadingPolicy.JoinableTaskContext,
                  project.Services.DataSourceRegistry,
                  true)
        {
            this.Project = project;
        }

        protected override async Task<ImmutableHashSet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            if (await this.Project
                .HasRemoteLinuxCapabilityAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                return new[] { Constants.Capabilities.RemoteLinuxCapability }.ToImmutableHashSet();
            }
            return ImmutableHashSet<string>.Empty;
        }
    }
}