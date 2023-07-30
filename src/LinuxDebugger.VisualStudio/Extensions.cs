using Microsoft.VisualStudio.Shell;

namespace LinuxDebugger.VisualStudio;

public static class Extensions
{
    public static ValueTask<int> RunCommandAsync(
        this IVsSshClient client,
        string cmd,
        CancellationToken cancellationToken)
    => client.RunCommandAsync(cmd, VsSshClient.COMMAND_TIMEOUT, cancellationToken);
    public static void FileAndForget(this JoinableTask task, Func<Exception, bool>? fileOnlyIf = null)
    {
        if (task.IsCompleted) return;
        task.Task.FileAndForget(fileOnlyIf);
    }
    public static void FileAndForget(this Task task, Func<Exception, bool>? fileOnlyIf = null)
    {
        var joinableTask = ThreadHelper.JoinableTaskFactory.RunAsync(async() =>
        {
            try
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                await task.ConfigureAwait(continueOnCapturedContext: false);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
            }
            catch (Exception ex)
            when (fileOnlyIf?.Invoke(ex) ?? true)
            {
                var text = ex.ToString();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _ = ActivityLog.TryLogError(ex.Message, text);
            }
        });
    }

}
