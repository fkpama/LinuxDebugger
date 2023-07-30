using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using Microsoft.VisualStudio.PlatformUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LinuxDebugger.ProjectSystem.Serialization
{
    internal sealed class DownloadMetadata : ModelBase
    {
        [JsonProperty(nameof(Path))]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string? EffectivePath
        {
            get => !string.IsNullOrWhiteSpace(Path)
                ? Path : null;
            set => this.Path = value;
        }
        [JsonIgnore]
        public string? Path { get; set; }
        public bool OpenInEditor { get; set; }
        public bool Required { get; set; }
        [JsonIgnore]
        public bool ShouldSerializeAsObject
        {
            get => this.OpenInEditor || this.Required;
        }
        public string Serialize() => Serialize(this);
        public static string Serialize(DownloadMetadata metadata)
        {
            return JsonConvert.SerializeObject(metadata, Settings);
        }

        public static DownloadMetadata Deserialize(string json)
        {
            var meta = JsonConvert.DeserializeObject<DownloadMetadata>(json, Settings);
            Assumes.NotNull(meta);
            return meta;
        }

        internal static DownloadMetadata Deserialize(IReadOnlyDictionary<string, string> di)
        {
            var data = new DownloadMetadata();
            var collection = new JsonPropertyCollection(typeof(DownloadMetadata));
            foreach (var item in di)
            {
                var prop = collection.GetClosestMatchProperty(item.Key);
                if (prop is null)
                {
                    continue;
                }
                object value = item.Value;
                if (prop.PropertyType == typeof(bool))
                {
                    if(bool.TryParse(item.Value, out var val))
                    {
                        value = val ? Boxes.BooleanTrue : Boxes.BooleanFalse;
                    }
                }
                prop?.ValueProvider?.SetValue(data, value);
            }
            return data;

            static bool getBool(KeyValuePair<string, object> kvp)
            {
                var val = false;
                if (kvp.Value is not string s)
                {

                }
                else if (s.IsPresent())
                {
                    try
                    {
                        val = XmlConvert.ToBoolean(s!.ToLowerInvariant());
                    }
                    catch (FormatException)
                    {
                    }
                }
                return val;
            }
        }
        internal static DownloadMetadata Deserialize(IReadOnlyDictionary<string, object> di)
        {
            var data = new DownloadMetadata();
            var collection = new JsonPropertyCollection(typeof(DownloadMetadata));
            foreach (var item in di)
            {
                var prop = collection.GetClosestMatchProperty(item.Key);
                prop?.ValueProvider?.SetValue(data, item.Value);
                //if (string.Equals(item.Key, nameof(Path), StringComparison.OrdinalIgnoreCase))
                //{
                //    data.Path = item.Value as string;
                //}
                //else if (string.Equals(item.Key, nameof(OpenInEditor), StringComparison.OrdinalIgnoreCase))
                //{
                //    data.OpenInEditor = getBool(item);
                //}
                //else if (string.Equals(item.Key, nameof(Required), StringComparison.OrdinalIgnoreCase))
                //{
                //    data.Required = getBool(item);
                //}
                //else
                //{
                //    // TODO: Log
                //}
            }
            return data;

            static bool getBool(KeyValuePair<string, object> kvp)
            {
                var val = false;
                if (kvp.Value is not string s)
                {

                }
                else if (s.IsPresent())
                {
                    try
                    {
                        val = XmlConvert.ToBoolean(s!.ToLowerInvariant());
                    }
                    catch (FormatException)
                    {
                    }
                }
                return val;
            }
        }

        internal static DownloadMetadata Deserialize(JObject jo)
        {
            return jo.ToObject<DownloadMetadata>()!;
        }

        internal static DownloadMetadata Parse(string value)
            => value.Trim().StartsWith("{")
            ? Deserialize(value)
            : new() { Path = value };

        internal static DownloadMetadata FromObject(object value)
        {
            if (value is string s)
            {
                return new() { Path = s };
            }
            else if (value is JObject j)
            {
                return Deserialize(j);
            }
            else if (value is IReadOnlyDictionary<string, object> dictionary)
            {
                return Deserialize(dictionary);
            }
            else if (value is IReadOnlyDictionary<string, string> dict2)
            {
                return Deserialize(dict2);
            }

            throw new Exception();
        }
    }
}
