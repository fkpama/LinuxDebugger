using LinuxDebugger.ProjectSystem.Serialization;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.AdditionalDeploymentFiles, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class PreExecUploadFilesValueProvider : KeyValuePairSettingValueProvider
    {
        protected override string SettingName => Constants.ProfileParams.AdditionalDeploymentFiles;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.PostExecDownloadFile, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class PostExecDownloadFilesValueProvider : KeyValuePairSettingValueProvider
    {
        private readonly Dictionary<string, string> cache = new();
        protected override string SettingName => Constants.ProfileParams.PostExecDownloadFile;
        public override string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            if (launchProfile.OtherSettings?
                .TryGetValue(this.SettingName, out var files) != true
                || files is null)
                return null!;

            if (files is not IReadOnlyDictionary<string, object> dict)
            {
                return string.Empty;
            }

            var str = new Dictionary<string, string>();
            foreach(var item in  dict)
            {
                DownloadMetadata data;
                if (item.Value is string s)
                {
                    data = new DownloadMetadata{ Path = s };
                    str.Add(item.Key, data.Serialize());
                }
                else if (item.Value is IReadOnlyDictionary<string, object> di)
                {
                    data = DownloadMetadata.Deserialize(di);
                }
                else if (item.Value is JObject jo)
                {
                    data = DownloadMetadata.Deserialize(jo);
                }
                else if (item.Value is DownloadMetadata dl)
                {
                    data = dl;
                }
                else if (item.Value is null)
                {
                    data = null;
                }
                else
                {
                    continue;
                }
                str[item.Key] = data?.Serialize();
            }

            return LaunchProfileEnvironmentVariableEncoding.Format(str);
        }
        public override void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                launchProfile.OtherSettings?.Remove(this.SettingName);
                return;
            }

            lock (cache)
            {
                try
                {
                    LaunchProfileEnvironmentVariableEncoding
                        .ParseIntoDictionary(propertyValue, cache);

                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in cache)
                    {
                        if (kvp.Key.IsMissing())
                        {
                            // TODO: 
                            continue;
                        }
                        if (kvp.Value.Trim().StartsWith("{"))
                        {
                            var result = DownloadMetadata.Deserialize(kvp.Value);
                            if (result.ShouldSerializeAsObject)
                            {
                                dict[kvp.Key] = result.ToJObject();
                            }
                            else
                            {
                                dict[kvp.Key] = result.Path!;
                            }
                        }
                        else
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }
                    if (dict.Count > 0)
                        launchProfile.OtherSettings[propertyName] = dict;
                }
                finally
                {
                    cache.Clear();
                }
            }
        }
    }
    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.EnvironementVariables, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class EnvironmentVariableExtensionsValueProvider : ILaunchProfileExtensionValueProvider
    {
        private readonly IProjectShim project;

        [ImportingConstructor]
        public EnvironmentVariableExtensionsValueProvider(IProjectShim project)
        {
            this.project = project;
        }
        public string OnGetPropertyValue(string propertyName,
                                         ILaunchProfile launchProfile,
                                         ImmutableDictionary<string, object> globalSettings,
                                         Rule? rule)
        {
            if (launchProfile.EnvironmentVariables is null
                || launchProfile.EnvironmentVariables.Count == 0)
            {
                var env = this.project.GetDebugEnvironmentVariables();
                return LaunchProfileEnvironmentVariableEncoding.Format(env);
            }
            var key = LaunchProfileEnvironmentVariableEncoding.Format(launchProfile);
            return key;
        }

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                launchProfile.EnvironmentVariables?.Clear();
                return;
            }
            LaunchProfileEnvironmentVariableEncoding
                .ParseIntoDictionary(propertyValue, launchProfile.EnvironmentVariables);
        }
    }
}