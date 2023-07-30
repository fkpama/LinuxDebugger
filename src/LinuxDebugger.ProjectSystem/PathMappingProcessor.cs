using LinuxDebugger.ProjectSystem.Deployment;
using LinuxDebugger.ProjectSystem.Deployment.Providers;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem
{
    internal class PathMappingProcessor
    {
        private List<SimplePathMapping>? outItems;
        public DirectoryHandle ProjectDir { get; }
        public DirectoryHandle? SolutionDir { get; set; }
        public PathMappingRoot Root { get; private set; } = PathMappingRoot.Project;
        public IReadOnlyList<string>? OutsideItems { get; }
        internal bool HasOutsideFiles => this.outItems?.Count > 0;

        public PathMappingProcessor(DirectoryHandle projectDir, DirectoryHandle? solutionDir)
        {
            this.ProjectDir = projectDir;
            this.SolutionDir = solutionDir;
        }

        internal void AddRange(IEnumerable<SimplePathMapping> v)
        {
            foreach(var item in v) this.Add(item);
        }
        internal void Add(SimplePathMapping v)
        {
            if (PathUtil.IsDescendant(this.ProjectDir, v.Target))
            {
                this.ProjectDir.Add(v);
                return;
            }

            if (this.SolutionDir is not null
                && PathUtil.IsDescendant(this.SolutionDir, v.Target))
            {
                this.Root = PathMappingRoot.Solution;
                this.SolutionDir.Add(v);
                return;
            }

            (outItems ??= new()).Add(v);
        }
    }
}
