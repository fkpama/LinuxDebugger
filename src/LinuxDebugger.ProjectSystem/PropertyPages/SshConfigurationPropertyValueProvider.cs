using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    [ExportInterceptingPropertyValueProvider("SshConnectionConfigurationProperty", ExportInterceptingPropertyValueProviderFile.ProjectFile)]
    internal sealed class SshConfigurationPropertyValueProvider : InterceptingPropertyValueProviderBase
        //: IInterceptingPropertyValueProvider
    {
        public override Task<string?> OnSetPropertyValueAsync(string propertyName, string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string>? dimensionalConditions = null)
        {
            return TaskResult.Null<string>();
        }

        public override Task<string> OnGetEvaluatedPropertyValueAsync(string propertyName, string evaluatedPropertyValue, IProjectProperties defaultProperties)
        {
            return TaskResult.EmptyString;
        }

        public override Task<string> OnGetUnevaluatedPropertyValueAsync(string propertyName, string unevaluatedPropertyValue, IProjectProperties defaultProperties)
        {
            return TaskResult.EmptyString;
        }
    }
}
