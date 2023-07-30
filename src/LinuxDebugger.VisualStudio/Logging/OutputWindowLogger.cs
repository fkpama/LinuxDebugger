using System.Diagnostics;
using LinuxDebugger.VisualStudio.Platform;
using Microsoft.VisualStudio.Shell.Interop;

namespace LinuxDebugger.VisualStudio.Logging
{
    public abstract class Logger : ILogger
    {
        protected virtual bool RunOnMainThread { get; }
        public static ILogger None => NoneLogger.Instance;
        public static ILogger OutputWindow(Guid paneId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var svc = ServiceProvider
                .GlobalProvider
                .GetService<SVsOutputWindow, IVsOutputWindow>();
            return new OutputWindowLogger(paneId, svc);
        }
        private TextWriter? textWriter;
        private readonly IThreadHelper threadHelper;

        public LogLevel LogLevel { get; } = LogLevel.Verbose;

        internal TextWriter TextWriter
        {
            get
            {
                if (this.textWriter is null)
                {
                    this.textWriter = this.RunOnMainThread
                        ? this.threadHelper.RunOnUiThread(() => this.Open())
                        : this.Open();
                    Debug.Assert(this.textWriter is not null);
                }
                return this.textWriter!;
            }
        }
        private protected Logger(IThreadHelper? threadHelper = null)
        {
            this.threadHelper = threadHelper ?? PlatformServices.ThreadService;
        }
        protected abstract TextWriter Open();

        public void LogVerbose(string message)
        {
            this.Log(LogLevel.Verbose, message);
        }

        public void LogInformation(string message)
        {
            this.Log(LogLevel.Information, message);
        }

        public void LogWarning(string message)
        {
            this.Log(LogLevel.Warning, message);
        }

        private void Log(LogLevel information, string message)
        {
            var dt = DateTimeOffset.Now;
            if (this.LogLevel > information)
            {
                return;
            }
            var msg = dt.ToString("HH:mm:ss.ffffff");
            var lvl = logLevelLabel(information);
            var text = $"{msg} [{lvl}]: {message}";
            _ = Task.Run(async () =>
            {
                if (this.RunOnMainThread)
                    await this.threadHelper.SwitchToMainThreadAsync(CancellationToken.None);
                await this.TextWriter.WriteLineAsync(text).ConfigureAwait(false);
                await this.TextWriter.FlushAsync();
            });
            //if (RunOnMainThread)
            //{
            //    this.threadHelper.RunOnUiThread(() =>
            //    {
            //        this.TextWriter.WriteLine($"{msg} [{lvl}]: {message}");
            //    })
            //}
            //else
            //    this.TextWriter.WriteLine($"{msg} [{lvl}]: {message}");
        }

        public void LogError(string msg)
        {
            this.Log(LogLevel.Error, msg);
        }

        public void LogError(Exception exception, string msg)
        {
            this.Log(LogLevel.Error, $"{msg}\n{exception}");
        }

        private static string logLevelLabel(LogLevel information)
            => information switch
            {
                >= LogLevel.Error => "ERR",
                >= LogLevel.Warning => "WARN",
                >= LogLevel.Information => "INFO",
                _ => "VERBOSE",
            };

        private sealed class NoneLogger : Logger
        {
            private static NoneLogger? s_logger;

            internal static NoneLogger Instance
            {
                get => s_logger ??= new();
            }

            private NoneLogger() { }

            protected override TextWriter Open()
            {
                return TextWriter.Null;
            }
        }
    }

    internal sealed class FileLogger : Logger
    {
        protected override TextWriter Open()
        {
            throw new NotImplementedException();
        }
    }
    internal sealed class OutputWindowLogger : Logger
    {
        private readonly Lazy<IVsOutputWindowPane> pane;
        private readonly IThreadHelper threadHelper;

        protected override bool RunOnMainThread => true;

        private IVsOutputWindowPane? Pane
        {
            get => this.pane.Value;
        }
        public OutputWindowLogger(Guid guid,
                                  IVsOutputWindow window,
                                  IThreadHelper? threadHelper = null)
        {
            this.threadHelper = threadHelper ?? PlatformServices.ThreadService;
            this.pane = new(() =>
            {
                var pane = this.threadHelper.RunOnUiThread(() =>
                {
                    var id = guid;
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
                    if (ErrorHandler.Failed(window.GetPane(ref id, out var pane)))
                    {
                        _ = ErrorHandler.ThrowOnFailure(window
                        .CreatePane(ref id,
                        LinuxConstants.CrossPlatformOutputWindowPaneLabel,
                        0,
                        0));
                        _ = ErrorHandler.ThrowOnFailure(
                        window.GetPane(ref id, out pane));
                        int hr;
                        if (!ErrorHandler.Failed(hr = pane.Activate()))
                        {
                            // TODO
                        }
                    }
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
                    return pane;
                });
                return pane;
            });
        }

        protected override TextWriter Open()
        {
            var pane = this.Pane;
            return new OutputWindowTextWriter(pane);
        }
    }
}
