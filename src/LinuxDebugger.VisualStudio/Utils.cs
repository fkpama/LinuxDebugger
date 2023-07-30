#pragma warning disable ISB001 // Dispose of proxies

using System.Diagnostics;
using System.Text;
using liblinux.Persistence;
using liblinux.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Terminal;

namespace LinuxDebugger.VisualStudio
{
    internal static class Utils
    {
        internal static ConnectionInfo GetInfos(int id)
        {
            if (!new ConnectionInfoStore().TryGetById(id, out ConnectionInfo infos2))
            {
                throw new Exception();
            }
            return infos2;
        }

        internal static AsyncLazy<ITerminalService> GetTerminalService(this IAsyncServiceProvider services,
            JoinableTaskFactory? taskFactory = null)
        {
            taskFactory ??= ThreadHelper.JoinableTaskFactory;
            return new(async () =>
            {
                var container = await services
                .GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>()
                .ConfigureAwait(false);
                var serviceBroker = container.GetFullAccessServiceBroker();
                var svc = await serviceBroker
                .GetProxyAsync<ITerminalService>(TerminalServiceDescriptors.TerminalServiceDescriptor, VsShellUtilities.ShutdownToken)
                .ConfigureAwait(false);
                if (svc is null)
                {
                    throw new ServiceUnavailableException(typeof(ITerminalService));
                }
                return svc;
            }, taskFactory);
        }

        internal static bool WaitOutputReceived(this IShell shell,
                                                   string text,
                                                   TimeSpan timeout,
                                                   TimeSpan interval,
                                                   Func<bool>? busyWaitFunc,
                                                   CancellationToken cancellationToken = default)
        {
            bool ret;
            for (var start = Stopwatch.GetTimestamp();
                !(ret = shell.WaitOutputReceived(text, interval))
                && Stopwatch.GetTimestamp() - start < timeout.Ticks;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (busyWaitFunc?.Invoke() ?? false)
                    break;
            }
            return ret;
        }
        internal static Task<bool> WaitOutputReceivedAsync(this IShell shell,
                                                   string text,
                                                   TimeSpan timeout,
                                                   TimeSpan interval,
                                                   CancellationToken cancellationToken)
            => Task.Run(() => shell.WaitOutputReceived(text, timeout, interval, null, cancellationToken))
            .WithCancellation(cancellationToken);
        internal static Task<bool> WaitOutputReceivedAsync(this IShell shell,
                                                   string text,
                                                   TimeSpan timeout,
                                                   TimeSpan interval,
                                                   StringBuilder currentOutput,
                                                   int busyWaitCount,
                                                   CancellationToken cancellationToken)
        {
            var count = 0;
            return Task.Run(() => shell.WaitOutputReceived(text, timeout, interval, () =>
            {
                if (count >= busyWaitCount)
                    return false;
                var str = currentOutput.ToString();
                return str.IndexOf(text, StringComparison.Ordinal) >= 0;
            }, cancellationToken))
            .WithCancellation(cancellationToken);
        }
    }
}
#pragma warning restore ISB001 // Dispose of proxies