using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    [Export(typeof(IPropertyEditor))]
    [AppliesTo($"{Constants.Capabilities.RemoteLinuxCapability} & {ProjectCapabilities.LaunchProfiles}")]
    [ExportMetadata("Name", "FileUploadMapping")]
    sealed class FileUploadMappingEditor : PropertyEditorBase
    {
        public FileUploadMappingEditor()
            : base("UploadMappingDataTemplate") { }
    }
}
