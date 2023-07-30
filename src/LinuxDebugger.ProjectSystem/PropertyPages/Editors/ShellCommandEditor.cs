using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    [Export(typeof(IPropertyEditor))]
    [AppliesTo($"{Constants.Capabilities.RemoteLinuxCapability} & {ProjectCapabilities.LaunchProfiles}")]
    [ExportMetadata("Name", "ShellCommand")]
    sealed class ShellCommandEditor : PropertyEditorBase
    {
        public ShellCommandEditor()
            : base("ShellCommandDataTemplate") { }
    }
}
