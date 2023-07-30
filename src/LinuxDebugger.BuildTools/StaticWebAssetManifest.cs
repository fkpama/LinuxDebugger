using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace LinuxDebugger.BuildTools;

public interface IStaticWebAssetNode
{
    string RelPath { get; }
    SimplePathMapping GetFullPath();
    SimplePathMapping GetFullPath(string root);
}

partial class StaticAssetsManager
{

    public sealed class StaticWebAssetManifest
    {
        internal static readonly StringComparer PathComparer =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        public List<string> ContentRoots { get; set; } = new();

        public StaticWebAssetNode Root { get; set; } = null!;
        internal static StaticWebAssetManifest Parse(Stream manifest)
        {
            using var sr = new StreamReader(manifest,
                                             encoding: Encoding.UTF8,
                                             detectEncodingFromByteOrderMarks: false,
                                             bufferSize: 2048,
                                             leaveOpen: true);
            var str = sr.ReadToEnd();
            return Parse(str);
        }
        internal static StaticWebAssetManifest Parse(string manifestContent)
        {
            var manifest = JsonConvert.DeserializeObject<StaticWebAssetManifest>(manifestContent)!;
            manifest.Root.IsRoot = true;
            return manifest;
        }

        internal void Serialize(Stream fstream)
        {
            var serialized = JsonConvert.SerializeObject(this);
            var bts  = Encoding.UTF8.GetBytes(serialized);
            fstream.Write(bts, 0, bts.Length);
        }
    }

    //private sealed class OSBasedCaseConverter : JsonConverter<Dictionary<string, StaticWebAssetNode>>
    //{
    //    public override Dictionary<string, StaticWebAssetNode>? ReadJson(JsonReader reader,
    //                                                                Type objectType,
    //                                                                Dictionary<string, StaticWebAssetNode>? existingValue,
    //                                                                bool hasExistingValue,
    //                                                                JsonSerializer serializer)
    //    {
    //        var parsed = serializer.Deserialize<Dictionary<string, StaticWebAssetNode>>(reader);
    //        if (parsed is null)
    //        {
    //            return null;
    //        }
    //        var result = new Dictionary<string, StaticWebAssetNode>(StaticWebAssetManifest.PathComparer);
    //        MergeChildren(parsed, result);
    //        return result;

    //        static void MergeChildren(
    //            IDictionary<string, StaticWebAssetNode> newChildren,
    //            IDictionary<string, StaticWebAssetNode> existing)
    //        {
    //            foreach (var kvp in newChildren)
    //            {
    //                var key = kvp.Key;
    //                var value = kvp.Value;
    //                if (!existing.TryGetValue(key, out var existingNode))
    //                {
    //                    existing.Add(key, value);
    //                }
    //                else
    //                {
    //                    if (value.Patterns != null)
    //                    {
    //                        if (existingNode.Patterns == null)
    //                        {
    //                            existingNode.Patterns = value.Patterns;
    //                        }
    //                        else
    //                        {
    //                            if (value.Patterns.Length > 0)
    //                            {
    //                                var newList = new StaticWebAssetPattern[existingNode.Patterns.Length + value.Patterns.Length];
    //                                existingNode.Patterns.CopyTo(newList, 0);
    //                                value.Patterns.CopyTo(newList, existingNode.Patterns.Length);
    //                                existingNode.Patterns = newList;
    //                            }
    //                        }
    //                    }

    //                    if (value.Children != null)
    //                    {
    //                        if (existingNode.Children == null)
    //                        {
    //                            existingNode.Children = value.Children;
    //                        }
    //                        else
    //                        {
    //                            if (value.Children.Count > 0)
    //                            {
    //                                MergeChildren(value.Children, existingNode.Children);
    //                            }
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }

    //    public override void WriteJson(JsonWriter writer, Dictionary<string, StaticWebAssetNode>? value, JsonSerializer serializer)
    //    {
    //        serializer.Serialize(writer, value);
    //    }
    //}

    public sealed class StaticWebAssetNode : IStaticWebAssetNode
    {
        StaticWebAssetNode? parent;
        private string? name;
        private Origin origin;

        [JsonProperty("Asset", Order = 1)]
        public StaticWebAssetMatch? Match { get; set; }

        internal bool IsRoot;

        [JsonIgnore]
        public string RelPath
        {
            get
            {
                if (this.parent is not null && !this.parent.IsRoot)
                    return Path.Combine(parent.RelPath, this.name);

                Debug.Assert(this.name is not null);
                return this.name!;
            }
        }

        public SimplePathMapping GetFullPath()
        {
            return this.GetFullPath(this.origin);
        }

        public SimplePathMapping GetFullPath(string root)
        {
            if (this.Match is null)
            {
                return new(this.RelPath, this.RelPath);
            }
            Debug.Assert(this.origin.Index == this.Match.ContentRoot);
            var srcPath = Path.Combine(this.origin, this.Match.Path);
            Debug.Assert(this.parent is not null);
            string targetPath = this.parent is not null && !this.parent.IsRoot
                ? Path.Combine(root, this.parent!.RelPath, this.name)
                : Path.Combine(root, this.name);
            return new(srcPath, targetPath);
        }

        //[JsonConverter(typeof(OSBasedCaseConverter))]
        [JsonProperty(Order = 0)]
        public Dictionary<string, StaticWebAssetNode>? Children { get; set; }

        [JsonProperty(Order = 3)]
        public StaticWebAssetPattern[]? Patterns { get; set; }

        internal IEnumerable<IStaticWebAssetNode> GetDescendants(Origin origin)
        {
            if (this.Children is null)
                yield break;
            foreach(var child in this.Children)
            {
                child.Value.parent = this;
                child.Value.name = child.Key;
                if (child.Value.Match is not null
                    && child.Value.Match.ContentRoot == origin.Index)
                {
                    child.Value.origin = origin;
                    yield return child.Value;
                }
                else
                {
                    foreach (var descendant in child.Value.GetDescendants(origin))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        //[MemberNotNullWhen(true, nameof(Children))]
        internal bool HasChildren() => Children != null && Children.Count > 0;

        //[MemberNotNullWhen(true, nameof(Patterns))]
        internal bool HasPatterns() => Patterns != null && Patterns.Length > 0;
    }

    public sealed class StaticWebAssetMatch
    {
        [JsonProperty("ContentRootIndex")]
        public int ContentRoot { get; set; }

        [JsonProperty("SubPath")]
        public string Path { get; set; } = null!;
    }

    public sealed class StaticWebAssetPattern
    {
        [JsonProperty("ContentRootIndex")]
        public int ContentRoot { get; set; }

        public int Depth { get; set; }

        public string Pattern { get; set; } = null!;
    }
}
