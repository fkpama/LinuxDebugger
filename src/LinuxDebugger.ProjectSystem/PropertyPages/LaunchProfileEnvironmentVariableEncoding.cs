using System.Globalization;
using System.Reflection;
using LinuxDebugger.ProjectSystem.Serialization;
using Newtonsoft.Json.Linq;

namespace LinuxDebugger.ProjectSystem
{
    internal static class LaunchProfileEnvironmentVariableEncoding
    {

        //static readonly Type EnvVarType = Type.GetType("Microsoft.VisualStudio.ProjectSystem.Debug.LaunchProfileEnvironmentVariableEncoding, Microsoft.VisualStudio.ProjectSystem.Managed");
        static Type EnvVarType
             => Assembly.GetType("Microsoft.VisualStudio.ProjectSystem.Debug.LaunchProfileEnvironmentVariableEncoding");
        //static readonly Type KeyValuePairType = Type.GetType("Microsoft.VisualStudio.ProjectSystem.Debug.KeyValuePairListEncoding, Microsoft.VisualStudio.ProjectSystem.Managed");
        static Assembly? s_managedAssembly;
        static Type KeyValuePairType
             => Assembly.GetType("Microsoft.VisualStudio.ProjectSystem.Debug.KeyValuePairListEncoding");

        static Assembly Assembly
        {
            get
            {
                if (s_managedAssembly is null)
                {
                    s_managedAssembly = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .FirstOrDefault(x => string.Equals(x.GetName().Name, "Microsoft.VisualStudio.ProjectSystem.Managed", StringComparison.Ordinal));
                    Assumes.NotNull(s_managedAssembly);
                }
                return s_managedAssembly;
                //s_managedAssembly = Assembly.Load("Microsoft.VisualStudio.ProjectSystem.Managed, Version=17.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                //Assembly.GetAssembly()
            }
        }
        static MethodInfo ParseDictionaryMethodInfo
        {
            get
            {
                var method = EnvVarType.GetMethod("ParseIntoDictionary");
                return method;
            }
        }
        static MethodInfo FormatMethodInfo
        {
            get
            {
                var method = EnvVarType.GetMethod("Format");
                return method;
            }
        }
        static MethodInfo KvpFormatMethodInfo
        {
            get
            {
                var method = KeyValuePairType.GetMethod("Format");
                return method;
            }
        }

        public static string Format(IReadOnlyDictionary<string, string> dict)
        {
            return (string)KvpFormatMethodInfo
                .Invoke(null, new object[] { dict.Select(x => (x.Key, x.Value ?? string.Empty)) });
        }
        public static string Format(IReadOnlyDictionary<string, object> dict)
        {
            return (string)KvpFormatMethodInfo
                .Invoke(null, new object[] { dict.Select(x => (x.Key, (string)(x.Value ?? string.Empty))) });
        }
        public static string Format(ILaunchProfile? profile)
        {
            return (string)FormatMethodInfo.Invoke(null, new object?[] { profile });
        }

        public static void ParseIntoDictionary(string value, Dictionary<string, string> dictionary)
        {
            ParseDictionaryMethodInfo.Invoke(null, new object[] { value, dictionary });
        }
        public static Dictionary<string, string> ParseIntoDictionary(string value)
        {
            var dict = new Dictionary<string, string>();
            ParseIntoDictionary(value, dict);
            return dict;
        }

        internal static ImmutableDictionary<string, object?> DeserializeSettings(string value)
        {
            var jo = JObject.Parse(value);
            var result = process(jo, new());
            return result;

            static ImmutableDictionary<string, object?> process(JObject jo, Dictionary<string, object?> dict)
            {
                foreach(var item in jo)
                {
                    object? value;
                    if (item.Value?.Type == JTokenType.Object)
                    {
                        var dict2 = new Dictionary<string, object?>();
                        value = process((JObject)item.Value, dict2);
                    }
                    else
                    {
                        value = item.Value?.ToObject<object>();
                    }
                    if (value is not null)
                    dict.Add(item.Key, value);
                }
                var ret = dict.ToImmutableDictionary();
                dict.Clear();
                return ret;
            }
        }

        internal static string FormatDownload(string? remotePath, bool required, bool openInEditor)
        {
            return DownloadMetadata.Serialize(new()
            {
                OpenInEditor = openInEditor,
                Required = required,
                Path = remotePath
            });
        }

        const char ShellCommandLineSeparator = '@';
        internal static string
            FormatCommandLine(string? commandLine, bool ignoreExitCode)
            => ignoreExitCode.ToString(CultureInfo.InvariantCulture)
            + ShellCommandLineSeparator
            + commandLine;
        internal static (string? Command, bool IgnoreExitCode)
            ParseCommandLine(string? str)
        {
            if (str.IsMissing())
                return (null, false);
            Assumes.NotNull(str);
            var idx = str.IndexOf('@');
            string? command = null, strIgnore;
            bool ignore = false;
            if (idx < 0)
            {
                command = str;
            }
            else if ((strIgnore = str.Substring(0, idx)).IsPresent()
                && bool.TryParse(strIgnore, out ignore))
            {
                command = str.Substring(idx + 1);
            }
            return (command, ignore);
        }
    }
}