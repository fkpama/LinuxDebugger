using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.ExePath, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class ExecutablePathValueProvider : ILaunchProfileExtensionValueProvider
    {
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.ExecutablePath!;

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.ExecutablePath = propertyValue;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.CommandLineArguments, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class CommandLineArgsValueProvider : ILaunchProfileExtensionValueProvider
    {
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.CommandLineArgs!;

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.CommandLineArgs = propertyValue;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.WorkingDirectory, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class WorkingDirectoryValueProvider : ILaunchProfileExtensionValueProvider
    {
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.WorkingDirectory!;

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.WorkingDirectory = propertyValue;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.BrowserUrl, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class ApplicationUrlValueProvider : ILaunchProfileExtensionValueProvider
    {
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.LaunchUrl!;

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.LaunchUrl = propertyValue;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.LaunchBrowser, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class LaunchBrowserValueProvider : ILaunchProfileExtensionValueProvider
    {
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
            => launchProfile.LaunchBrowser.ToString();

        public void OnSetPropertyValue(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            var val = false;
            if (propertyValue.IsPresent())
                bool.TryParse(propertyValue.ToLowerInvariant(), out val);

            launchProfile.LaunchBrowser = val;
        }
    }
}
