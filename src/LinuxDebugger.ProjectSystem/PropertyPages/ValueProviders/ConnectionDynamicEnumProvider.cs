using System.Collections.ObjectModel;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [ExportDynamicEnumValuesProvider("ProjectConnectionsProvider")]
    [AppliesTo(Constants.Capabilities.RemoteLinuxCapability)]
    internal sealed class ConnectionDynamicEnumProvider : IDynamicEnumValuesProvider
    {
        private ProjectConnectionEnumValuesGenerator? generator;
        private ObservableCollection<IEnumValue>? values = null;
        private readonly ISshConnectionService connectionService;
        private readonly IProjectThreadingService threadingService;
        private readonly ILaunchSettingsProvider3 provider;

        [ImportingConstructor]
        public ConnectionDynamicEnumProvider([Import(ExportContractNames.Scopes.ProjectService)]ISshConnectionService connectionService,
            IProjectThreadingService threadingService,
            ILaunchSettingsProvider provider)
        {
            this.connectionService = connectionService;
            this.threadingService = threadingService;
            this.provider = (ILaunchSettingsProvider3)provider;
            connectionService.Manager.ConnectionsChanged += this.onConnectionChanged;
        }

        private async void onConnectionChanged(object sender, ConnectEventArgs args)
        {
            if (args.Operation == ConnectionChangedOperation.Removed)
            {
                if (this.values is null)
                    return;

                var item = this.values
                        .FirstOrDefault(x => string.Equals(x.DisplayName,
                        args.Hostname, StringComparison.OrdinalIgnoreCase));

                this.values.Remove(item);
            }
            else
            {
                if (this.values is not null)
                {
                    var count = this.values.Count;
                    if (this.values.Count > 0 && values[values.Count - 1].IsAddConnectionChoice())
                    {
                        count--;
                    }
                    this.values.Insert(count, new PageEnumValue(new()
                    {
                        Name = args.Id,
                        DisplayName = args.Hostname
                    }));
                }
            }

            var cancellationToken = VsShellUtilities.ShutdownToken;
            await this.provider
                .UpdateProfilesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IDynamicEnumValuesGenerator> GetProviderAsync(IList<NameValuePair>? options)
        {
            if (this.generator is null)
            {
                if (this.values is null)
                {
                    this.values = new()
                    {
                        new PageEnumValue(new()
                        {
                            Name = Constants.ProfileParams.DefaultConnectionName,
                            DisplayName = "Default",
                            Description = "Default Connection"
                        })
                    };

                    var connections = await this.connectionService.Manager
                        .GetConnectionInfosAsync(VsShellUtilities.ShutdownToken)
                        .ConfigureAwait(false);
                    foreach( var connection in connections )
                    {
                        this.values.Add(new PageEnumValue(new()
                        {
                            DisplayName = connection.Hostname,
                            Name = connection.Id
                        }));
                    }

                    this.values.Add(new PageEnumValue(new()
                    {
                        DisplayName = "<Create New>",
                        Name = Constants.ProfileParams.AddConnectionValue
                    }));
                }
                this.generator = new(this.values);
            }
            return this.generator;
        }

        internal sealed class ProjectConnectionEnumValuesGenerator
            : IDynamicEnumValuesGenerator
        {
            public bool AllowCustomValues { get; }
            private readonly ObservableCollection<IEnumValue> values;

            public ProjectConnectionEnumValuesGenerator(ObservableCollection<IEnumValue> values)
            {
                this.values = values;
            }

            public Task<ICollection<IEnumValue>> GetListedValuesAsync()
            {
                return Task.FromResult<ICollection<IEnumValue>>(this.values);
            }

            public Task<IEnumValue?> TryCreateEnumValueAsync(string userSuppliedValue)
            {
                if (this.values is null)
                    return TaskResult.Null<IEnumValue>();
                var value = this.values?
                .FirstOrDefault(x => string.Equals(userSuppliedValue, x.Name, StringComparison.Ordinal));
                if (value is null)
                    return TaskResult.Null<IEnumValue>();

                return Task.FromResult<IEnumValue?>(value);
            }

        }
    }

    [ExportLaunchProfileExtensionValueProvider(Constants.ProfileParams.ConnectionId, ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ConnectionIdValueProvider : SettingsValueProviderBase
    {
        private readonly Lazy<ISshConnectionService> sshConnectionService;
        private readonly IProjectThreadingService threadingService;

        protected override string DefaultValue => Constants.ProfileParams.DefaultConnectionName;

        [ImportingConstructor]
        public ConnectionIdValueProvider(
            [Import(ExportContractNames.Scopes.ProjectService)]
            Lazy<ISshConnectionService> sshConnectionService,
            IProjectThreadingService threadingService)
        {
            this.sshConnectionService = sshConnectionService;
            this.threadingService = threadingService;
        }

        protected override bool IsDefault(string propertyValue)
             => base.IsDefault(propertyValue)
            || string.Equals(Constants.ProfileParams.DefaultConnectionName, propertyValue);

        public override string OnGetPropertyValue(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            return base.OnGetPropertyValue(propertyName, launchProfile, globalSettings, rule);
        }

        public override void OnSetPropertyValue(string propertyName,
                                                string propertyValue,
                                                IWritableLaunchProfile launchProfile,
                                                ImmutableDictionary<string, object> globalSettings,
                                                Rule? rule)
        {
            if (propertyValue.IsPresent()
                && string.Equals(propertyValue,
                Constants.ProfileParams.AddConnectionValue,
                StringComparison.Ordinal))
            {

                //await showOptionPageAsync(cancellationToken);
                var current = this.OnGetPropertyValue(propertyName,
                                                      launchProfile.ToLaunchProfile(),
                                                      globalSettings,
                                                      rule);
                propertyValue = this.threadingService
                        .JoinableTaskFactory
                        .Run(async () =>
                        {
                            try
                            {
                                var conn2 =  await this.sshConnectionService
                                    .Value
                                    .Manager
                                    .AddConnectionAsync(VsShellUtilities.ShutdownToken)
                                    .ConfigureAwait(false);
                                return conn2?.Id.ToString() ?? current;
                            }
                            catch (OperationCanceledException)
                            {
                                return current;
                            }
                        });
            }
            base.OnSetPropertyValue(propertyName, propertyValue, launchProfile, globalSettings, rule);
        }

    }

}
