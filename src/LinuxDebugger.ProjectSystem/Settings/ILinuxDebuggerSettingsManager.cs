#pragma warning disable ISB001 // Dispose of proxies
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.RpcContracts.Settings;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Threading;
using LinuxDebugger.VisualStudio.Settings;

namespace LinuxDebugger.ProjectSystem
{
    public interface ILinuxDebuggerSettingsManager
    {
        ValueTask<LinuxDebuggerSettings> GetSettingsAsync(CancellationToken cancellationToken);
        ValueTask<LinuxDebuggerSettings> GetSettingsAsync(ConfiguredProject project, CancellationToken shutdownToken);
        ValueTask SaveAsync(CancellationToken shutdownToken);
    }

    [Export(typeof(ILinuxDebuggerSettingsManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class LinuxDebuggerSettingManager : ILinuxDebuggerSettingsManager
    {
        private readonly AsyncLazy<ISettingsManager3?> settingsManagerLazy;
        private readonly AsyncLazy<ISettingsManager2?> settingsManager2Lazy;
        private LinuxDebuggerSettings? settings;
        private readonly JoinableTaskFactory joinableTaskFactory;


        public LinuxDebuggerSettingManager()
        {
            this.joinableTaskFactory = ThreadHelper.JoinableTaskFactory;
            this.settingsManagerLazy = new AsyncLazy<ISettingsManager3?>(CreateSettingsManager3Async,
                this.joinableTaskFactory);
            this.settingsManager2Lazy = new AsyncLazy<ISettingsManager2?>(CreateSettingsManager2Async,
                this.joinableTaskFactory);
        }

        public ValueTask<LinuxDebuggerSettings> GetSettingsAsync(ConfiguredProject project, CancellationToken cancellationToken)
            => GetSettingsAsync(cancellationToken);
        public ValueTask<LinuxDebuggerSettings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            if (this.settings is not null)
                return new(this.settings);

            return new(Task.Run(async () =>
            {
                var mgr = await this.settingsManagerLazy.GetValueAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new ServiceUnavailableException(typeof(ISettingsManager3));

                var settings = new LinuxDebuggerSettings();

                foreach(var property in TypeDescriptor.GetProperties(settings).Cast<PropertyDescriptor>())
                {
                    var name = settings.GetSharedSettingsStorePath(property);
                    var value = await this.GetValueOrDefaultAsync(mgr, name, cancellationToken).ConfigureAwait(false);
                    if (value is not null)
                    {
                        SetPropertyValue(property, settings, value);
                    }
                }

                this.settings = settings;
                return this.settings;
            }));
        }

        private void SetPropertyValue(PropertyDescriptor descriptor, LinuxDebuggerSettings settings, object value)
        {
            if (value is not null)
            {
                try
                {
                    var obj = (descriptor.PropertyType.IsEnum ? Enum.ToObject(descriptor.PropertyType, value) : ((!descriptor.PropertyType.IsAssignableFrom(value.GetType())) ? Convert.ChangeType(value, descriptor.PropertyType, CultureInfo.InvariantCulture) : value));
                    descriptor.SetValue(settings, obj);
                }
                catch (FormatException) { }
                catch (InvalidCastException) { }
                catch (OverflowException) { }
            }
        }

        public async ValueTask SaveAsync(CancellationToken cancellationToken)
        {
            if (this.settings is null)
                return;
            var mgr = await this.settingsManagerLazy.GetValueAsync(cancellationToken);
            if (mgr is null)
                return;

            foreach (var property in TypeDescriptor.GetProperties(settings).Cast<PropertyDescriptor>())
            {
                SaveSetting(mgr, property, settings);
            }
        }

        private void SaveSetting(ISettingsManager3 mgr, PropertyDescriptor property, LinuxDebuggerSettings settings)
        {
            var path = settings.GetSharedSettingsStorePath(property);
            object value2 = property.GetValue(settings);
            _ = joinableTaskFactory.RunAsync(async () =>
            {
                var cancellationToken = VsShellUtilities.ShutdownToken;

                var x = await this.settingsManager2Lazy.GetValueAsync(cancellationToken)
                .ConfigureAwait(false) ?? throw new NotImplementedException();
                var scope = StoreLogPropertyDefinition.Source.WithValue(GetType().FullName + ".SaveSettings");
                using var _ = x.StoreUpdateLogger.SetContext(scope);
                await mgr.SetValueAsync(path, value2, cancellationToken).ConfigureAwait(false);
            });
        }

        private async Task<object?> GetValueOrDefaultAsync(ISettingsManager3 mgr,
                                                           string name,
                                                           CancellationToken cancellationToken)
        {
            SettingRequestResult settingRequestResult = await mgr.GetValueAsync(name, cancellationToken);
            return settingRequestResult.Succeeded ? settingRequestResult.DeserializeValue<object>() : null;
        }
        private async Task<T?> CreateBrokeredServiceAsync<T>(ServiceRpcDescriptor descriptor)
            where T : class
        {
            try
            {
                var brokeredServiceContainer = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
                var serviceBroker = brokeredServiceContainer.GetFullAccessServiceBroker();
                var settingsManager = await serviceBroker
                    .GetProxyAsync<T>(descriptor)
                    .ConfigureAwait(false);
                if (settingsManager is null)
                {
                    await serviceBroker.ReportMissingServiceAsync(descriptor.Moniker.Name, "SettingsManager", descriptor.Moniker);
                }

                return settingsManager;
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(GetType().FullName, ex.ToString());
                return null;
            }
        }
        private Task<ISettingsManager3?> CreateSettingsManager3Async()
            => CreateBrokeredServiceAsync<ISettingsManager3>(VisualStudioServices.VS2019_4.SettingsManager);

        private async Task<ISettingsManager2?> CreateSettingsManager2Async()
        {
            var svc = await AsyncServiceProvider.GlobalProvider
                .GetServiceAsync<SVsSettingsPersistenceManager, ISettingsManager2>()
                .ConfigureAwait(false);
            return svc;
        }

        // see: https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed.VS/ProjectSystem/VS/SVsSettingsPersistenceManager.cs
        [Guid("9B164E40-C3A2-4363-9BC5-EB4039DEF653")]
        internal interface SVsSettingsPersistenceManager { }
    }
}
#pragma warning restore ISB001 // Dispose of proxies