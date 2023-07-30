using System.Collections;
using System.Collections.Immutable;

namespace LinuxDebugger.BuildTools;

public struct Origin
{
    internal int Index;
    public string Path { get; }
    public Origin(string path, int index)
    {
        this.Path = path;
        this.Index = index;
    }

    public static implicit operator string(Origin origin) => origin.Path;
    public static implicit operator int(Origin origin) => origin.Index;
}
public partial class StaticAssetsManager
{
    private readonly StaticWebAssetManifest manifest;
    private string[]? mappings;

    public int Count => this.manifest.ContentRoots.Count;

    public string FilePath { get; }

    public Origin this[int index]
        => new(this.manifest.ContentRoots[index], index);

    private StaticAssetsManager(StaticWebAssetManifest manifest, string filePath)
    {
        this.manifest = manifest;
        this.FilePath = filePath;
    }

    public static StaticAssetsManager Normalize(string fname)
    {
        var text = File.ReadAllText(fname);
        var manifest = StaticWebAssetManifest.Parse(text);
        return new(manifest, fname);
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(this.FilePath))
        {
            throw new InvalidOperationException();
        }
        this.Save(this.FilePath);
    }
    public void Save(string filePath)
    {
        using var fstream = File.OpenWrite(filePath);
        fstream.SetLength(0);
        this.Save(fstream);
        fstream.Flush();
    }
    public void Save(Stream stream)
    {
        lock (this.manifest)
        {
            string[]? originalMappings = null;
            if (this.mappings is not null)
            {
                for(var i = 0; i < this.mappings.Length; i++)
                {
                    var mapping = this.mappings[i];
                    if (!string.IsNullOrWhiteSpace(mapping))
                    {
                        originalMappings ??= this.manifest.ContentRoots.ToArray();
                        this.manifest.ContentRoots[i] = mapping;
                    }
                }
            }
            this.manifest.Serialize(stream);

            if (originalMappings is not null)
            {
                this.manifest.ContentRoots.Clear();
                this.manifest.ContentRoots.AddRange(originalMappings);
            }
        }
    }

    public IEnumerable<IStaticWebAssetNode> GetAllItems(Origin root)
        => this.manifest.Root.GetDescendants(root);

    public void Map(int index, string path)
    {
        this.mappings ??= new string[this.manifest.ContentRoots.Count];
        this.mappings[index] = path;
    }
}
