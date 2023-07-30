using System.Collections.Specialized;
using System.Diagnostics;
using LinuxDebugger.ProjectSystem.Build;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.ProjectSystem.Build;

namespace LinuxDebugger.ProjectSystem.Deployment.Providers
{
    public enum DeploymentType
    {
        Build,
        Publish
    }
    public interface IDeploymentSession
    {
        DeploymentType DeploymentType { get; }
        IProjectSnapshot Project { get; }
        string ProjectDirectory { get; }
        string LocalOutputDirectory { get; }
        string SolutionDirectory { get; }
        IDictionary<object, object> Properties { get; }
        IOutputGroupsService OutputGroups { get; }
        string DeploymentDirectory { get; }
        string LocalRootDirectory { get; }
        ValueTask<ITaskItem[]> GetCopyToOutputDirectoryItemsAsync(CancellationToken cancellationToken);
        ValueTask<SimplePathMapping[]> GetOutputGroupItemsAsync(string built, CancellationToken cancellationToken);
        ValueTask<string> GetPropertyAsync(string property, CancellationToken cancellationToken);
        ValueTask<T> GetPropertyAsync<T>(string property, CancellationToken cancellationToken);
    }

    public abstract class DirectoryBaseHandle
    {
        public abstract string Path { get; }

        internal virtual void SetPath(string path)
        {
            throw new NotImplementedException();
        }
        public static implicit operator DirectoryBaseHandle(string path)
            => path is null ? null! : new DirectoryHandle(path);
        public static implicit operator string(DirectoryBaseHandle path)
            => path.Path;
    }

    public sealed class PathCombination : DirectoryBaseHandle
    {
        private readonly DirectoryBaseHandle[] components;

        public override string Path => this.components.Length switch
        {
            1 => this.components[0].Path,
            2 => LinuxPath.Combine(this.components[0], this.components[1]),
            _ => LinuxPath.Combine(this.components[0], this.components[1], this.components.Skip(2).Select(x => x.Path).ToArray())
        };

        public PathCombination(params DirectoryBaseHandle[] components)
        {
            this.components = components;
        }
    }

    public sealed class DirectoryReference : DirectoryBaseHandle
    {
        private readonly DirectoryBaseHandle handle;

        public override string Path => this.handle.Path;
        public DirectoryReference(DirectoryBaseHandle handle)
        {
            this.handle = handle;
        }

        internal override void SetPath(string path)
            => this.handle.SetPath(path);
    }
    public sealed class DirectoryHandle : DirectoryBaseHandle
    {
        private HashSet<SimplePathMapping>? files;
        private string path;
        public int Count => this.files?.Count ?? 0;
        public override string Path => this.path;
        public string? RemotePath { get; internal set; }

        internal DirectoryHandle(string path)
        {
            this.path = path;
        }

        internal override void SetPath(string path)
        {
            this.path = path;
        }

        internal IEnumerable<SimplePathMapping> GetFiles()
        {
            Assumes.NotNull(this.RemotePath);
            if (this.files is null)
                yield break;
            foreach (var fi in this.files)
            {
                string relPath, srcPath = fi.Source;
                if (!System.IO.Path.IsPathRooted(fi.Source))
                {
                    srcPath = System.IO.Path.Combine(this.Path, fi.Source);
                }
                if (System.IO.Path.IsPathRooted(fi.Target))
                {
                    Debug.Assert(PathUtil.IsDescendant(this.Path, fi.Target));
                    relPath = PathUtil.MakeRelative(this.Path, fi.Target);
                }
                else
                {
                    relPath = fi.Target;
                }

                yield return new(srcPath, LinuxPath.MakeUnixPath(this.RemotePath, relPath));
            }
        }
        internal void Add(SimplePathMapping path)
        {
            (this.files ??= new(PathMappingComparer.Instance)).Add(path);
        }
        public static implicit operator string(DirectoryHandle handle)
            => handle.Path;
        public static implicit operator DirectoryHandle(string path)
            => path.IsMissing() ? null : new(path);

    }
    public interface IRemoteDeploymentProvider
    {
        ValueTask<SimplePathMapping[]> GetAdditionalFileAsync(IDeploymentSession session, CancellationToken cancellationToken);
        ValueTask ProcessAsync(IDeploymentSession session, CancellationToken cancellationToken);
        //ValueTask ProcessAsync(IDeploymentSession session, CancellationToken cancellationToken);
    }
}
