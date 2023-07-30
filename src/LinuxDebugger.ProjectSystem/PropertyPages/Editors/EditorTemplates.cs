using System.Windows;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    public static class EditorTemplates
    {
        #region EditorMetadata property

        public static readonly DependencyProperty EditorMetadataProperty =
            DependencyProperty.RegisterAttached("EditorMetadata",
                typeof(IReadOnlyDictionary<string, string>),
                typeof(EditorTemplates),
                new FrameworkPropertyMetadata
                {
                    OverridesInheritanceBehavior = true,
                    Inherits = true
                });
        public static object GetEditorMetadata(DependencyObject @object)
        {
            return @object.GetValue(EditorMetadataProperty);
        }
        public static void SetEditorMetadata(DependencyObject @object, object value)
        {
            @object.SetValue(EditorMetadataProperty, value);
        }

        #endregion EditorMetadata property

        #region Template Property

        public static readonly DependencyProperty TemplateProperty =
            DependencyProperty.RegisterAttached("Template", typeof(DataTemplate), typeof(EditorTemplates),
                new FrameworkPropertyMetadata
                {
                    OverridesInheritanceBehavior = true,
                    Inherits = true
                });
        public static object GetTemplate(DependencyObject @object)
        {
            return @object.GetValue(TemplateProperty);
        }
        public static void SetTemplate(DependencyObject @object, object value)
        {
            @object.SetValue(TemplateProperty, value);
        }

        #endregion Template Property

        internal static IPathMappingEncoding ModeToEcoding(MappingMode mode)
            => mode switch
            {
                MappingMode.Upload => UploadFileMappingEncoding.Instance,
                MappingMode.Download => DownloadFileMappingEncoding.Instance,
                _ => throw new Exception()
            };

    }
}
