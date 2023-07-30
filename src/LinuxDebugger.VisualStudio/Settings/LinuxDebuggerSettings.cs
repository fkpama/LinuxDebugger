using System.ComponentModel;

namespace LinuxDebugger.VisualStudio.Settings
{
    [SharedSettings(SharedSettingsStorePath, false)]
    public sealed class LinuxDebuggerSettings
    {
        internal const string SharedSettingsStorePath = "Debugger.RemoteLinux.CrossPlatform";
        public string? VsDbgDirectory { get; set; } = LinuxConstants.DefaultVsdbgBasePath;
        public bool UseSsh { get; set; } = true;
        public string? AdapterExePath { get; set; }
        public bool AutoInstallVsDbg { get; set; } = true;
        public string RemoteProjectDirectory { get; set; } = "~/projects";
        public string RemoteDotnetPath { get; set; } = LinuxConstants.DefaultDotNetPath;

        //
        // Summary:
        //     Returns the shared settings store path for the given property.
        public string GetSharedSettingsStorePath(PropertyDescriptor property)
            => GetSharedSettingsStorePath(this, property);
        public static string GetSharedSettingsStorePath(LinuxDebuggerSettings settings, PropertyDescriptor property)
        {
            var storePath = SharedSettingsStorePath;
            return $"{storePath}.{property.Name}";
        }
    }
}
