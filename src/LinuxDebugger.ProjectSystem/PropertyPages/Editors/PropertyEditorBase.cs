using System.Windows;
using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    internal abstract class PropertyEditorBase : IPropertyEditor
    {
        private static readonly Lazy<ResourceDictionary> s_lazyResources = new(() =>
        {
            if (!UriParser.IsKnownScheme("pack"))
                UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);
            var asm = typeof(PropertyEditorBase).Assembly.FullName;
            return new ResourceDictionary()
            {
                Source = new Uri($"pack://application:,,,/{asm};component/PropertyPages/Editors/PropertyEditorTemplates.xaml", UriKind.RelativeOrAbsolute)
            };
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly Lazy<DataTemplate>? lazyPropertyDataTemplate,
            lazyUnconfiguredDataTemplate,
            lazyConfiguredDataTemplate;

        public virtual bool ShowUnevaluatedValue { get; }
        public DataTemplate? PropertyDataTemplate
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread(nameof(PropertyDataTemplate));
                return this.lazyPropertyDataTemplate?.Value;
            }
        }
        public DataTemplate? UnconfiguredDataTemplate
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread(nameof(UnconfiguredDataTemplate));
                return this.lazyUnconfiguredDataTemplate?.Value;
            }
        }
        public DataTemplate? ConfiguredDataTemplate
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread(nameof(ConfiguredDataTemplate));
                return this.lazyConfiguredDataTemplate?.Value;
            }
        }
        public virtual object? DefaultValue { get; }
        public bool IsPseudoProperty { get; }

        protected PropertyEditorBase(string? propertyDataTemplateName,
            string? unconfiguredDataTemplateName = null,
            string? configuredDataTemplateName = null)

        {
            if (propertyDataTemplateName.IsPresent())
            {
                this.lazyPropertyDataTemplate = new(() =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var template = (DataTemplate)s_lazyResources.Value[propertyDataTemplateName];
                    Assumes.NotNull(template);
                    return template;
                });
            }
            if (unconfiguredDataTemplateName.IsPresent())
            {
                this.lazyUnconfiguredDataTemplate = new(() =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var template = (DataTemplate)s_lazyResources.Value[unconfiguredDataTemplateName];
                    Assumes.NotNull(template);
                    return template;
                });
            }
            if (configuredDataTemplateName.IsPresent())
            {
                this.lazyConfiguredDataTemplate = new(() =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    var template = (DataTemplate)s_lazyResources.Value[configuredDataTemplateName];
                    Assumes.NotNull(template);
                    return template;
                });
            }
        }

        public virtual bool IsChangedByEvaluation(string unevaluatedValue, object? evaluatedValue, ImmutableDictionary<string, string> editorMetadata)
        {
            throw new NotImplementedException();
        }

        public bool ShouldShowDescription(int valueCount)
            => true;
    }
}
