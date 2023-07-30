using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem;

internal interface IProgressIndicator : IProgress<string>
{
    CancellationToken CancellationToken { get; }
    void UpdateProgress(string v);
}

public sealed class ProgressIndicator<TResult> : IProgressIndicator, IDisposable
{
    private ThreadedWaitDialogHelper.Session? session;
    private JoinableTaskFactory taskFactory = ThreadHelper.JoinableTaskFactory;
    private readonly TaskCompletionSource<TResult?> completionSource;

    private readonly CancellationTokenSource cancellation;

    public string Title { get; } = "Remote Linux";
    public string ProgressText { get; private set; }
    public bool IsCancellable { get; }
    public TimeSpan ShowDelay { get; }

    public CancellationToken CancellationToken => this.cancellation.Token;

    public Task<TResult?> ProgressTask => completionSource.Task;

    public ProgressIndicator(string title,
                             string text,
                             CancellationToken cancellation,
                             double delayToShowTimeout = -1,
                             bool isCancellable = true)
    {
        completionSource = new();
        this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        this.Title = title;
        this.ProgressText = text;
        this.IsCancellable = isCancellable;
        this.ShowDelay = delayToShowTimeout < 0
            ? TimeSpan.FromSeconds(1)
            : (delayToShowTimeout == 0
            ? default
            : TimeSpan.FromMilliseconds(delayToShowTimeout));
    }

    public async Task ShowProgressAsync(CancellationToken cancellationToken)
    {
        var factory = await AsyncServiceProvider.GlobalProvider
            .GetServiceAsync<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (factory is not null)
        {
            cancellationToken.Register(this.cancellation.Cancel);
            this.session =
            factory.StartWaitDialog(
                this.Title,
                initialProgress: new(this.Title,
                                     this.ProgressText,
                                     isCancelable: this.IsCancellable),
                delayToShowDialog: this.ShowDelay);
            this.session.UserCancellationToken
                .Register(() =>
                {
                    this.cancellation.Cancel();
                    this.SetException(new OperationCanceledException(this.session.UserCancellationToken));
                });
        }
    }

    public async Task UpdateProgressAsync(string progressText, int current, int total)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (session is not null)
        {
            session.Progress.Report(new(waitMessage: this.Title,
                                        progressText: progressText,
                                        statusBarText: null,
                                        isCancelable: true,
                                        currentStep: current,
                                        totalSteps: total));
        }
    }
    public void UpdateProgress(string progressText)
    {
        if (this.session is null)
        {
            this.ProgressText = progressText;
            this.ShowProgressAsync(CancellationToken.None).Forget();
        }
        else
        {
            Task.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (session is not null)
                {
                    session.Progress.Report(new(waitMessage: this.Title,
                                                progressText: progressText,
                                                statusBarText: null,
                                                isCancelable: true));
                }
            }).Forget();
        }
    }

    void close()
    {
        ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (this.session is not null)
            {
                this.session.Dispose();
                this.session = null;
            }
        });
    }
    public void CloseProgress(TResult? result)
    {
        Task.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                if (this.session is not null)
                {
                    this.session.Dispose();
                    this.session = null;
                }
            }
            finally
            {
                completionSource.SetResult(result);
            }
        }).FileAndForget();
    }

    public void Report(string value)
    {
        UpdateProgress(value);
    }

    internal void SetException(Exception ex)
    {
        try
        {
            if (this.session is not null)
            {
                this.session.Dispose();
                this.session = null;
            }
        }
        finally
        {
            this.completionSource.TrySetException(ex);
        }
    }

    void IDisposable.Dispose()
    {
        //if (VsShellUtilities.ShellIsShuttingDown)
        //    return;
        var cancellationToken = VsShellUtilities.ShutdownToken;
        this.taskFactory.Run(async () =>
        {
            await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (this.session is not null)
            {
                this.session.Dispose();
                this.session = null;
            }
        });
        GC.SuppressFinalize(this);
    }
}
