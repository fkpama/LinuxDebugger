using LinuxDebugger.ProjectSystem.Serialization;

namespace LinuxDebugger.ProjectSystem;

internal readonly struct PathMapping
{
    private readonly string? target;
    public PathMapping(string source, DownloadMetadata target)
    {
        this.Source = source;
        this.Metadata = target;
        this.target = target.Path;
    }

    public PathMapping(PathMapping mapping, string path) : this()
    {
        this.Source = mapping.Source;
        this.Metadata = mapping.Metadata;
        this.target = path;
    }

    public string Source { get; }
    public DownloadMetadata Metadata { get; }
    public string? Target => this.target ?? this.Metadata.Path;
    internal PathMapping WithPath(string path) => new(this, path);
}
