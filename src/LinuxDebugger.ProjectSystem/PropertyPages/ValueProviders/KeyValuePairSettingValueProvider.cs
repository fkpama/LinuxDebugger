using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    internal abstract class KeyValuePairSettingValueProvider : ILaunchProfileExtensionValueProvider
    {
        protected abstract string SettingName { get; }
        public virtual string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            object? files;
            if (launchProfile.OtherSettings?
                .TryGetValue(this.SettingName, out files) != true
                || files is null)
                return null!;

            string ret = string.Empty;
            if (files is IReadOnlyDictionary<string, object> dict)
            {
                var key = LaunchProfileEnvironmentVariableEncoding.Format(dict);
                ret = key;
            }
            else if (files is IReadOnlyDictionary<string, string> dict2)
            {
                var key = LaunchProfileEnvironmentVariableEncoding.Format(dict2);
                ret = key;
            }
            else if (files is string s)
            {
                ret = s;
            }
            else
            {
                // TODO: warn
            }
            return ret;
        }

        public virtual void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                launchProfile.OtherSettings?.Remove(this.SettingName);
                return;
            }

            Dictionary<string, string> dict = new();
            LaunchProfileEnvironmentVariableEncoding.ParseIntoDictionary(propertyValue, dict);
            if (dict.Count == 0)
                launchProfile.OtherSettings.Remove(this.SettingName);
            launchProfile.OtherSettings[this.SettingName] = dict;
        }
    }
}