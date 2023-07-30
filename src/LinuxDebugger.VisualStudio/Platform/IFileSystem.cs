namespace LinuxDebugger.VisualStudio.Platform
{
    public readonly struct MainThreadAwaitableWrapper
    {
        private readonly JoinableTaskFactory.MainThreadAwaitable inner;

        public MainThreadAwaitableWrapper(JoinableTaskFactory.MainThreadAwaitable inner)
        {
            this.inner = inner;
        }

        public void GetAwaiter()
        {
        }
    }
    internal interface IThreadHelper
    {
        void RunOnUiThread(Action action);
        T RunOnUiThread<T>(Func<T> action);
        void ThrowIfNotOnUIThread();
        JoinableTaskFactory.MainThreadAwaitable SwitchToMainThreadAsync(CancellationToken cancellationToken);
    }
    internal interface IFileSystem
    {
        void WriteAllText(string jsonPath, string json);
    }
}
