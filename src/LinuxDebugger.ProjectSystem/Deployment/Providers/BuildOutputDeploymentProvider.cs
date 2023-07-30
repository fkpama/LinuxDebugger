using LinuxDebugger.VisualStudio.Logging;
using Microsoft.VisualStudio.Shell.Interop;

namespace LinuxDebugger.ProjectSystem.Deployment.Providers
{
    sealed class PathMappingComparer : IEqualityComparer<SimplePathMapping>
    {
        internal static readonly PathMappingComparer Instance = new();
        public bool Equals(SimplePathMapping x, SimplePathMapping y)
        {
            if (!PathHelper.IsSamePath(x.Source, y.Source))
            {
                return false;
            }
            return string.Equals(x.Target, y.Target, StringComparison.Ordinal);
        }

        public int GetHashCode(SimplePathMapping obj)
        {
            var scode1 = obj.Source.IsPresent()
                ? Path.GetFullPath(obj.Source).GetHashCode()
                : 0;
            var scode2 = obj.Target?.GetHashCode() ?? 0;

            return 123475 + scode1 + scode2;
        }
    }
    [Export(typeof(IRemoteDeploymentProvider))]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    [ExportMetadata("Name", "Build Output")]
    sealed class BuildOutputDeploymentProvider : RemoteDeploymentBaseProvider
    {
        private readonly Lazy<ILogger> _log;
        private ILogger log => _log.Value;

        [ImportingConstructor]
        public BuildOutputDeploymentProvider(LoggerService log)
        {
            this._log = new(() => log.GetLogger<BuildOutputDeploymentProvider>());
        }

        public override async ValueTask<SimplePathMapping[]> GetAdditionalFileAsync(IDeploymentSession session, CancellationToken cancellationToken)
        {
            var result = new List<SimplePathMapping>();
            var lst = new List<Task>
            {
                //Task.Run(async () =>
                //{
                //    try
                //    {
                //        var items = await session
                //        .GetCopyToOutputDirectoryItemsAsync(cancellationToken)
                //        .ConfigureAwait(false);
                //        lock (result)
                //        {
                //            result.AddRange(items
                //                .Select(x => x.GetFullPath())
                //                .Select(x => new SimplePathMapping(x!)));
                //        }
                //    }
                //    catch(Exception ex)
                //    {
                //        ex.RethrowIfCritical();
                //        log.LogError(ex, "GetCopy");
                //        // the getOutputGroup calls should fill in
                //    }
                //}, cancellationToken),
                getOutputGroup(BuildOutputGroup.Built),
                getOutputGroup(BuildOutputGroup.Symbols),
                getOutputGroup(BuildOutputGroup.LocalizedResourceDlls),
                getOutputGroup(BuildOutputGroup.ContentFiles),
                getOutputGroup(Constants.MSBuild.OutputGroups.ReferenceCopyLocalPaths)
            };

            await Task.WhenAll(lst).ConfigureAwait(false);
            return result.Distinct(PathMappingComparer.Instance).ToArray();
            async Task getOutputGroup(string name)
            {
                var items = await session
                    .GetOutputGroupItemsAsync(name, cancellationToken)
                    .ConfigureAwait(false);
                lock (result)
                {
                    result.AddRange(items);
                }
            }

        }
    }
}
