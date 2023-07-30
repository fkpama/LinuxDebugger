using System.ComponentModel.Design;
using static Microsoft.VisualStudio.Threading.JoinableTaskFactory;
using VSThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace LinuxDebugger.VisualStudio.Platform;

internal static class PlatformServices
{
    private sealed class FileSystemImpl : IFileSystem
    {
        public void WriteAllText(string jsonPath, string json)
            => File.WriteAllText(jsonPath, json);
    }

    private sealed class ThreadHelperImpl : IThreadHelper
    {
        private struct VoidResult { }
        public void RunOnUiThread(Action action)
            => this.RunOnUiThread<VoidResult>(() =>
            {
                action();
                return default;
            });

        public T RunOnUiThread<T>(Func<T> action)
            => VSThreadHelper.JoinableTaskContext.IsOnMainThread
            ? action()
            : VSThreadHelper.JoinableTaskFactory
                .Run(async () =>
                {
                    await VSThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var result = action();
                    return result;
                });

        public MainThreadAwaitable SwitchToMainThreadAsync(CancellationToken cancellationToken)
        {
#pragma warning disable VSTHRD004 // Await SwitchToMainThreadAsync
            return VSThreadHelper
                .JoinableTaskFactory
                .SwitchToMainThreadAsync(cancellationToken);
#pragma warning restore VSTHRD004 // Await SwitchToMainThreadAsync
        }

        public void ThrowIfNotOnUIThread()
            => VSThreadHelper.ThrowIfNotOnUIThread();
    }

    internal static IFileSystem FileSystem { get; set; } = new FileSystemImpl();
    internal static IThreadHelper ThreadService { get; set; } = new ThreadHelperImpl();
    internal static CommandID? DebugAdapterHostCommandId { get; set; }
    internal static CommandID? DebugAdapterHostLoggingCommandId { get; set; }

}
