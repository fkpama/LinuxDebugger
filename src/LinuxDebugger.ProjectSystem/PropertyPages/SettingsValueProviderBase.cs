using System.Collections.Immutable;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    internal abstract class SettingsValueProviderBase : ILaunchProfileExtensionValueProvider
    {
        protected virtual string DefaultValue { get; } = string.Empty;

        public virtual string OnGetPropertyValue(string propertyName,
                                         ILaunchProfile launchProfile,
                                         ImmutableDictionary<string, object> globalSettings,
                                         Rule? rule)
        {
            if (launchProfile.OtherSettings is null
                || !launchProfile.OtherSettings.TryGetValue(propertyName, out var x)
                || string.IsNullOrWhiteSpace(x as string))
            {
                return DefaultValue;
            }
            return x!.ToString();
        }

        public virtual void OnSetPropertyValue(string propertyName,
                                       string propertyValue,
                                       IWritableLaunchProfile launchProfile,
                                       ImmutableDictionary<string, object> globalSettings,
                                       Rule? rule)
        {
            if (IsDefault(propertyValue))
            {
                launchProfile.OtherSettings.Remove(propertyName);
                OnPropertyRemoved(launchProfile, propertyName);
            }
            else
            {
                launchProfile.OtherSettings[propertyName] = propertyValue;
            }
        }

        protected virtual void OnPropertyRemoved(IWritableLaunchProfile profile, string propertyName) { }

        protected virtual bool IsDefault(string propertyValue)
            => string.IsNullOrWhiteSpace(propertyValue);
    }
}
