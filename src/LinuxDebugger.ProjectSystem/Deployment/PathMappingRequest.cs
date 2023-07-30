namespace LinuxDebugger.ProjectSystem.Deployment
{
    public readonly struct PathMappingRequest
    {
        public string RelPath { get; }
        public PathMappingRoot From { get; }
        public PathMappingRequest(string relPath, PathMappingRoot solution)
        {
            this.RelPath = relPath;
            this.From = solution;
        }
    }
}
