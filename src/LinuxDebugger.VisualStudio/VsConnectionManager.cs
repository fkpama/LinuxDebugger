#pragma warning disable ISB001 // Dispose of proxies
using System.Collections.Concurrent;
using liblinux.Persistence;
using LinuxDebugger.VisualStudio.Infrastructure;
using Microsoft.VisualStudio.Linux.ConnectionManager;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace LinuxDebugger.VisualStudio
{
    public sealed class VsConnectionManager : IDisposable
    {
        private static readonly ServiceJsonRpcDescriptor s_serviceDescriptor = new(new ServiceMoniker("Microsoft.VisualStudio.VC.ConnectionInfoStoreService"), ServiceJsonRpcDescriptor.Formatters.UTF8, ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
        private IVsConnectionManager? connectionManager;
        private readonly JoinableTaskFactory taskFactory;
        private readonly AsyncLazy<IAsyncConnectionInfoStore> store;
        public event EventHandler<ConnectEventArgs>? ConnectionsChanged;
        private readonly ConcurrentDictionary<int, Task<VsSshClient>> sshClients = new();

        public VsConnectionManager(JoinableTaskFactory? taskFactory = null)
        {
            this.taskFactory ??= ThreadHelper.JoinableTaskFactory;
            this.store = new(this.getStoreAsync, taskFactory);
        }

        public async ValueTask<SshConnectionInfo> GetConnectionInfoAsync(int id, CancellationToken cancellation)
        {
            var store = await this.store.GetValueAsync(cancellation).ConfigureAwait(false);
            var info = await store.GetConnectionInfoAsync(id, cancellation).ConfigureAwait(false);
            return new(info);
        }
        public async ValueTask<IReadOnlyList<SshConnectionInfo>> GetConnectionInfosAsync(CancellationToken cancellation)
        {
            var store = await this.store
                .GetValueAsync(cancellation)
                .ConfigureAwait(false);
            var connections = await store.GetConnectionInfosAsync(cancellation).ConfigureAwait(false);
            return connections.Connections.Values
                .Select(x => new SshConnectionInfo(x))
                .ToArray();
        }
        public async ValueTask<IVsSshClient?> GetConnectionAsync(string hostname, CancellationToken cancellation)
        {
            var store = await this.store.GetValueAsync(cancellation).ConfigureAwait(false);
            var infos = await store.GetConnectionInfosAsync(cancellation).ConfigureAwait(false);
            var connection = infos.Connections.Values.FirstOrDefault(x => string.Equals(x.Host, hostname, StringComparison.OrdinalIgnoreCase));
            return connection is null ? null : new VsSshClient(new(connection), this.taskFactory);
        }
        public async ValueTask<IVsSshClient> GetDefaultConnectionAsync(CancellationToken cancellation)
        {
            var store = await this.store.GetValueAsync(cancellation).ConfigureAwait(false);
            var defaultConnection = await store.GetDefaultConnectionAsync(cancellation)
                .ConfigureAwait(false);
            if (!defaultConnection.HasValue)
            {
                throw new NotImplementedException();
            }
            var infos = await store
                .GetConnectionInfoAsync(defaultConnection.Value, cancellation)
                .ConfigureAwait(false);
            return new VsSshClient(new(infos), this.taskFactory);
        }

        void IDisposable.Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                (this.store as IDisposable)?.Dispose();
                if (this.connectionManager is not null)
                {
                    this.connectionManager.ConnectionRemoved -= this.onConnectionRemoved;
                    this.connectionManager.ConnectionAdded -= this.onConnectionAdded;
                }
            }
        }

        public async Task<IVsSshClient> GetConnectionAsync(int id, CancellationToken cancellationToken)
            => await this.InternalGetConnectionAsync(id, cancellationToken).ConfigureAwait(false);
        internal Task<VsSshClient> InternalGetConnectionAsync(int id, CancellationToken cancellationToken)
        {
            return this.sshClients.AddOrUpdate(id, async id =>
            {
                var store = await this.store
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false);

                var conn = await store
                .GetConnectionInfoAsync(id, cancellationToken)
                .ConfigureAwait(false);
                if (conn is null)
                {
                    throw new KeyNotFoundException($"Unknown connection id {conn}");
                }
                return new VsSshClient(new(conn), this.taskFactory);
            }, (x, y) => y);
        }

        public async ValueTask<SshConnectionInfo> AddConnectionAsync(CancellationToken cancellationToken)
        {
            _ = await this.store
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false); // ensure connectionManager created
            var mgr = this.connectionManager;
            Assumes.NotNull(mgr);

            var shell = await AsyncServiceProvider.GlobalProvider
                    .GetServiceAsync<SVsUIShell, IVsUIShell>()
                    .ConfigureAwait(false);
            IntPtr hwnd;
            if (shell is not null)
            {
                await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
                _ = ErrorHandler.ThrowOnFailure(shell.GetDialogOwnerHwnd(out hwnd));
                await TaskScheduler.Default;
            }
            else
            {
                hwnd = IntPtr.Zero;
            }
            SshConnectionInfo? connection = null;
            try
            {
                var result = mgr.ShowDialog();
                switch (result.DialogResult)
                {
                    case ConnectionManagerDialogResult.Cancelled:
                        throw new OperationCanceledException();
                    case ConnectionManagerDialogResult.Succeeded:
                        var store = await this.store
                        .GetValueAsync(cancellationToken)
                        .ConfigureAwait(false);

                        var conn = await store
                        .GetConnectionInfoAsync(result.StoredConnectionId, cancellationToken)
                        .ConfigureAwait(false);
                        connection = new(conn, result.ConnectionInfo);
                        break;
                    default:
                        // TODO: log
                        break;
                }
            }
            finally
            {
                if (hwnd != IntPtr.Zero)
                    NativeMethods.SetForegroundWindow(hwnd);
            }
            return connection;
        }

        private async Task<IAsyncConnectionInfoStore> getStoreAsync()
        {
            var sp = AsyncServiceProvider.GlobalProvider;
            var svc = await sp
                .GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();

            var mgr = await sp
                .GetServiceAsync<IVsConnectionManager, IVsConnectionManager>()
                .ConfigureAwait(false);

            mgr.ConnectionAdded += this.onConnectionAdded;
            mgr.ConnectionRemoved += this.onConnectionRemoved;
            this.connectionManager = mgr;

            var store = await svc.GetFullAccessServiceBroker()
            .GetProxyAsync<IAsyncConnectionInfoStore>(s_serviceDescriptor)
            .ConfigureAwait(false);
            Assumes.NotNull(store);
            return store;
        }

        private void onConnectionRemoved(object sender, ConnectionInfo e)
        {
            if (!this.store.IsValueCreated)
                return;
            var cancellationToken = VsShellUtilities.ShutdownToken;
            this.taskFactory.Run(async () =>
            {
                var store = await this.store
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false);

                var connections = await store
                .GetConnectionInfosAsync(cancellationToken)
                .ConfigureAwait(false);

                this.ConnectionsChanged?
                .Invoke(this, new(-1, ConnectionChangedOperation.Removed)
                {
                    Hostname = e.HostName,
                    AuthenticationMethod = (AuthenticationMethod)e.AuthenticationMode

                });
            });
        }

        private void onConnectionAdded(object sender, IConnectionManagerResult e)
        {
            this.ConnectionsChanged?.Invoke(this, new(e.StoredConnectionId,
                ConnectionChangedOperation.Added)
            {
                Hostname = e.ConnectionInfo?.HostName,
                AuthenticationMethod = (AuthenticationMethod)(e.ConnectionInfo?.AuthenticationMode ?? 0),
            });
        }

        internal async Task<RemoteSystem> GetRemoteSystemAsync(SshConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            var client = await this
                .InternalGetConnectionAsync(connectionInfo.InternalId, cancellationToken)
                .ConfigureAwait(false);
            return await client
                .GetRemoteSystemAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        //internal async Task<ConnectionInfo> GetConnectionInfoAsync(int connectionId, CancellationToken cancellationToken)
        //{
        //    var store = await this.store.GetValueAsync(cancellationToken).ConfigureAwait(false);
        //    var infos =  await store
        //        .GetConnectionInfoAsync(connectionId, cancellationToken)
        //        .ConfigureAwait(false);
        //    return Utils.GetInfos(infos.Id);
        //}
    }
}
#pragma warning restore ISB001 // Dispose of proxies