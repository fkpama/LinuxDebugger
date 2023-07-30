using System.Diagnostics;
using LinuxDebugger.BuildTools;
using LinuxDebugger.VisualStudio.Logging;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem.Deployment.Providers
{

    [Export(typeof(IRemoteDeploymentProvider))]
    [AppliesTo(Constants.Capabilities.RemoteRazorWebProject)]
    sealed class StaticWebAssetDeploymentProvider : IRemoteDeploymentProvider
    {
        static readonly object s_webAssetFilesKey = new();
        private readonly Lazy<ILogger> _log;
        private ILogger log => _log.Value;

        [ImportingConstructor]
        public StaticWebAssetDeploymentProvider(LoggerService log)
        {
            this._log = new Lazy<ILogger>(() => log.GetLogger<StaticAssetsManager>());
        }
        public async ValueTask<SimplePathMapping[]> GetAdditionalFileAsync(
            IDeploymentSession session,
            CancellationToken cancellationToken)
        {
            var outputDir = session.LocalOutputDirectory;
            var projectDir = session.ProjectDirectory;

            var items = await session
                .GetCopyToOutputDirectoryItemsAsync(cancellationToken)
                .ConfigureAwait(false);

            var names = items
                //.Select(x => x is null ? null : Path.Combine(outputDir, x.GetTargetPath()))
                .Where(x => x is not null && x.GetTargetPath()?.EndsWith(".staticwebassets.runtime.json", StringComparison.OrdinalIgnoreCase) == true)
                .Select(x => new
                {
                    Src = x.GetFullPath()!,
                    Dst = Path.Combine(session.LocalOutputDirectory, x.GetTargetPath())
                })
                .ToArray();

            var lst = new List<SimplePathMapping>();
            var dict = new Dictionary<string, StaticAssetsManager>();
            foreach (var fname in names)
            {
                if (!File.Exists(fname.Src))
                {
                    log.LogWarning($"File not found: {fname.Src}");
                    continue;
                }

                var mgr = StaticAssetsManager.Normalize(fname.Src!);
                lst.Add(fname.Dst!);
                dict.Add(fname.Dst!, mgr);
                for (var i = 0; i < mgr.Count; i++)
                {
                    var root = mgr[i];
                    foreach (var node in mgr.GetAllItems(root))
                    {
                        var fullPath = node.GetFullPath();
                        Debug.Assert(PathUtil.IsDescendant(root, fullPath.Source));
                        if (File.Exists(fullPath.Source))
                        {
                            lst.Add(fullPath);
                        }
                        else
                        {
                            log.LogWarning($"Static web asset file not found: {fullPath}");
                        }
                    }
                }
            }

            session.Properties[s_webAssetFilesKey] = dict;
            return lst.ToArray();
        }

        public ValueTask ProcessAsync(IDeploymentSession session,
                                      CancellationToken cancellationToken)
        {
            if (!session.Properties.TryGetValue(s_webAssetFilesKey, out var dictO))
            {
                throw new NotImplementedException();
            }

            var dict = (Dictionary<string, StaticAssetsManager>)dictO;

            foreach (var kvp in dict)
            {
                var fname = kvp.Key;
                var mgr = kvp.Value;

                for (var i = 0; i < mgr.Count; i++)
                {
                    var root = mgr[i];
                    if (!PathUtil.IsDescendant(session.LocalRootDirectory, root))
                    {
                        throw new NotImplementedException();
                    }

                    var rel = PathUtil.MakeRelative(session.LocalRootDirectory, root);
                    var remote = LinuxPath.MakeUnixPath(session.DeploymentDirectory, rel);
                    mgr.Map(i, remote);
                }

                mgr.Save(fname);
            }
            return default;
        }
    }
}
