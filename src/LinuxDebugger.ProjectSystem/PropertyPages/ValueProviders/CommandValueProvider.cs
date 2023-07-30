using LinuxDebugger.ProjectSystem.Serialization;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Newtonsoft.Json.Linq;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    internal abstract class CommandValueProvider : ILaunchProfileExtensionValueProvider
    {
        protected abstract string SettingsName { get; }
        public string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            if (launchProfile.OtherSettings?.TryGetValue(this.SettingsName, out var item)
                != true)
            {
                return string.Empty;
            }

            if (item is string s)
            {
                return s;
            }
            else if (item is JObject jo)
            {
                var (cmd, i) = CommandLine.Format(jo);
                if (cmd.IsMissing())
                    return string.Empty;
                return LaunchProfileEnvironmentVariableEncoding
                    .FormatCommandLine(cmd, i);
            }
            else if (item is CommandLine cmdLine)
            {
                return cmdLine.Format();
            }
            else if (item is IReadOnlyDictionary<string, object> dict)
            {
                cmdLine = CommandLine.Parse(dict);
                return cmdLine?.Format() ?? string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }

        public void OnSetPropertyValue(string propertyName,
                                       string propertyValue,
                                       IWritableLaunchProfile launchProfile,
                                       ImmutableDictionary<string, object> globalSettings,
                                       Rule? rule)
        {
            if (propertyValue.IsMissing())
            {
                launchProfile.OtherSettings.Remove(this.SettingsName);
                return;
            }

            var (command, ignoreExitCode) = LaunchProfileEnvironmentVariableEncoding
                .ParseCommandLine(propertyValue);

            if (command.IsMissing())
            {
                launchProfile.OtherSettings.Remove(this.SettingsName);
                return;
            }

            launchProfile.OtherSettings[propertyName] = ignoreExitCode
                ? new CommandLine
                {
                    Command = command,
                    IgnoreExitCode = ignoreExitCode,
                }.ToJObject()
                : command!;
        }

    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.CommandPre, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class PreCommandValueProvider : CommandValueProvider
    {
        protected override string SettingsName => Constants.ProfileParams.CommandPre;
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.CommandPost, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class PostCommandValueProvider : CommandValueProvider
    {
        protected override string SettingsName => Constants.ProfileParams.CommandPost;
    }
}
