using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LinuxDebugger.ProjectSystem.Serialization
{
    internal abstract class ModelBase
    {
        internal readonly static JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };

        internal readonly static JsonSerializer Serializer = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };
        internal JObject ToJObject()
        {
            var item = JObject.FromObject(this, Serializer);
            foreach(var prop in item.Properties().ToArray())
            {
                if (prop.HasValues
                    && prop.Value?.Type == JTokenType.Boolean
                    && !prop.Value.Value<bool>())
                    prop.Remove();
            }
            return item;
        }
    }
    internal sealed class CommandLine : ModelBase
    {
        public bool IgnoreExitCode { get; set; }
        public string? Command { get; set; }

        internal static (string? Command, bool IgnoreExitCode) Format(JObject jo)
        {
            bool ignoreExitCode = false;
            string? command = null;
            foreach(var kv in jo)
            {
                if (string.Equals(kv.Key, nameof(IgnoreExitCode), StringComparison.OrdinalIgnoreCase))
                {
                    ignoreExitCode = kv.Value?.ToObject<bool>() ?? false;
                }
                else if (string.Equals(kv.Key, nameof(Command), StringComparison.OrdinalIgnoreCase))
                {
                    command = kv.Value?.ToObject<string>();
                }
            }
            return (command, ignoreExitCode);
        }

        internal static CommandLine Parse(IReadOnlyDictionary<string, object> dict)
        {
            string? command = null;
            bool ignoreExitCode = false;

            foreach(var kvp in  dict)
            {
                if (string.Equals(kvp.Key, nameof(Command), StringComparison.OrdinalIgnoreCase))
                {
                    command = kvp.Value as string;
                }
                else if (string.Equals(kvp.Key, nameof(IgnoreExitCode), StringComparison.OrdinalIgnoreCase)
                    && kvp.Value is bool b)
                {
                    ignoreExitCode = b;
                }
            }

            return new()
            {
                Command = command,
                IgnoreExitCode = ignoreExitCode
            };
        }

        internal string Format()
        {
            if (this.Command.IsMissing())
                return string.Empty;
            return LaunchProfileEnvironmentVariableEncoding
                .FormatCommandLine(this.Command, this.IgnoreExitCode);
        }
    }
}
