using System.Diagnostics;
using System.Runtime.InteropServices;
using LinuxDebugger.VisualStudio;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem
{
    internal sealed partial class RemoteDebugLaunchTargetsProvider
    {
        sealed class DebuggerEvents : IVsDebuggerEvents,
            IDebugEventCallback2
        {
            private uint cookie;
            private IVsSshClient? client;
            private readonly AsyncLazy<IVsDebugger> debugger;
            private readonly AsyncLazy<IVsWebBrowsingService> webBrowser;
            private readonly IProjectThreadingService threadingService;
            public ILaunchProfile? Profile { get; private set; }
            public PathMapping[]? Downloads { get; private set; }
            public PathMapping[]? Uploads { get; private set; }

            public DebuggerEvents(IAsyncServiceProvider sp,
                                  IProjectThreadingService threadingService)
            {
                this.threadingService = threadingService;
                this.debugger = new(async () =>
                {
                    var debugger = await sp.GetServiceAsync<SVsShellDebugger, IVsDebugger>()
                .ConfigureAwait(false);
                    return debugger;
                }, threadingService.JoinableTaskFactory);

                this.webBrowser = new(async () =>
                {
                    var svc = await sp.GetServiceAsync<SVsWebBrowsingService, IVsWebBrowsingService>()
                    .ConfigureAwait(false);
                    return svc;
                }, threadingService.JoinableTaskFactory);
            }

            internal async Task SetupAsync(IVsSshClient sshClient,
                                           PathMapping[] downloads,
                                           PathMapping[] uploads,
                                           ILaunchProfile profile,
                                           CancellationToken cancellationToken)
            {
                this.client = sshClient;
                this.Profile = profile;
                this.Downloads = downloads;
                this.Uploads = uploads;
                Debug.Assert(this.cookie == 0);
                if (this.cookie == 0)
                {
                    var dbg = await this.debugger
                    .GetValueAsync(cancellationToken)
                    .ConfigureAwait(false);
                    await this.threadingService.SwitchToUIThread();
                    ErrorHandler.ThrowOnFailure(dbg
                        .AdviseDebuggerEvents(this, out this.cookie));
                    ErrorHandler.ThrowOnFailure(
                    dbg.AdviseDebugEventCallback(this));
                }
            }

            int IVsDebuggerEvents.OnModeChange(DBGMODE dbgmodeNew)
            {
                if (dbgmodeNew == DBGMODE.DBGMODE_Design)
                {
                    this.threadingService
                        .JoinableTaskFactory
                        .RunAsync(VsTaskRunContext.UIThreadBackgroundPriority, this.onDebuggingEndAsync)
                        .FileAndForget();
                }
                else if (dbgmodeNew == DBGMODE.DBGMODE_Run)
                {
                }
                return VSConstants.S_OK;
            }

            private async Task launchBrowserAsync()
            {
                var cancellationToken = VsShellUtilities.ShutdownToken;
                var url = this.Profile?.LaunchUrl;
                Assumes.NotNull(this.client);
                if (url.IsMissing())
                {
                    url = $"http://{this.client.Hostname}";
                }

                var svc = await this.webBrowser
                    .GetValueAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (svc is not null)
                {
                    await this.threadingService.SwitchToUIThread();
                    ErrorHandler.ThrowOnFailure(
                    svc.CreateExternalWebBrowser((uint)__VSCREATEWEBBROWSER.VSCWB_ReuseExisting, VSPREVIEWRESOLUTION.PR_Default, url));
                }
            }

            internal async Task DoUploadsAsync(CancellationToken cancellationToken)
            {
                if (!(this.Uploads?.Length > 0))
                {
                    return;
                }

                Assumes.NotNull(this.client);
                var lst = new List<Task>();
                foreach(var upload in this.Uploads)
                {
                    lst.Add(Task.Run(async () =>
                    {
                        var source = upload.Source;
                        var target = upload.Target;

                        var dir = LinuxPath.GetDirectoryName(target);
                        if (dir.IsPresent())
                        {
                            await this.client
                            .CreateDirectoryAsync(dir, cancellationToken)
                            .ConfigureAwait(false);
                        }

                        var targetPath = target;
                        if (await this.client
                            .DirectoryExistsAsync(target, cancellationToken)
                            .ConfigureAwait(false))
                        {
                            targetPath = LinuxPath.Combine(target, Path.GetFileName(source));
                        }

                        await this.client
                        .UploadAsync(source, targetPath, cancellationToken)
                        .ConfigureAwait(false);
                    }, cancellationToken));
                }

                await Task.WhenAll(lst).ConfigureAwait(false);
            }

            private async Task onDebuggingEndAsync()
            {
                try
                {
                    if (this.Downloads?.Length > 0)
                    {
                        await doDownloadAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    await this.resetAsync().ConfigureAwait(false);
                }
            }

            private async Task doDownloadAsync()
            {
                Assumes.NotNull(this.Downloads);
                Assumes.NotNull(this.client);
                using var indicator = new ProgressIndicator<int>(
                    "File downloads",
                    "Downloading files",
                    VsShellUtilities.ShutdownToken,
                    delayToShowTimeout: 0);
                await indicator.ShowProgressAsync(VsShellUtilities.ShutdownToken).ConfigureAwait(false);
                var cancellationToken = indicator.CancellationToken;
                var total = Downloads.Length;
                for (var i = 0; i < this.Downloads.Length; i++)
                {
                    var mapping = this.Downloads[i];
                    var target = mapping.Target;
                    if (mapping.Source.IsMissing()
                        || mapping.Target.IsMissing())
                    {
                        // TODO: log
                        continue;
                    }
                    var fname = Path.GetFileName(mapping.Target);
                    await indicator
                        .UpdateProgressAsync($"Dowloading {fname}", i + 1, total)
                        .ConfigureAwait(false);
                    Assumes.NotNull(target);
                    var required = mapping.Metadata?.Required ?? false;
                    try
                    {
                        var t = target;
                        var map = mapping;
                        var fi = await this.client
                            .DownloadAsync(map.Source, t, cancellationToken)
                            .ConfigureAwait(false);
                        if (fi is FileInfo f
                        && f.Exists
                        && map.Metadata?.OpenInEditor == true)
                        {
                            await this
                            .OpenFileAsync(f, cancellationToken)
                            .ConfigureAwait(false);
                        }
                    }
                    catch (FileNotFoundException) when(!required)
                    {
                    }
                    catch (DirectoryNotFoundException) when(!required)
                    {
                    }
                }
            }

            private async Task OpenFileAsync(FileInfo f, CancellationToken cancellationToken)
            {
                var svc = await AsyncServiceProvider
                    .GlobalProvider
                    .GetServiceAsync<SVsUIShellOpenDocument, IVsUIShellOpenDocument> ()
                    .ConfigureAwait(false);
                await this.threadingService.SwitchToUIThread();

                var viewId = VSConstants.LOGVIEWID.TextView_guid;
                ErrorHandler.ThrowOnFailure(
                svc.OpenDocumentViaProject(f.FullName,
                ref viewId,
                out _,
                out _,
                out _,
                out var frame));
                ErrorHandler.ThrowOnFailure(frame.Show());
            }

            private async Task resetAsync(CancellationToken cancellationToken = default)
            {
                Assumes.NotNull(this.debugger);
                if (this.cookie > 0)
                {
                    var dbg = await this.debugger
                        .GetValueAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!this.threadingService.IsOnMainThread)
                        await this.threadingService.SwitchToUIThread();
                    ErrorHandler.ThrowOnFailure(dbg.UnadviseDebuggerEvents(this.cookie));
                    ErrorHandler.ThrowOnFailure(dbg.UnadviseDebugEventCallback(this));
                    this.cookie = 0;
                    this.Profile = null;
                    this.Downloads = null;
                    this.Uploads = null;
                }
            }

            internal bool CanExecute(ILaunchProfile profile)
                => this.Profile == profile
                && (this.Uploads?.Length > 0
                || this.Downloads?.Length > 0);

            internal Task ResetAsync(CancellationToken cancellationToken)
                => this.resetAsync(cancellationToken);

            int IDebugEventCallback2.Event(IDebugEngine2 pEngine,
                                           IDebugProcess2 pProcess,
                                           IDebugProgram2 pProgram,
                                           IDebugThread2 pThread,
                                           IDebugEvent2 pEvent,
                                           ref Guid riidEvent,
                                           uint dwAttrib)
            {
                var objectList = new List<object>()
                {
                    pEngine, pProcess, pProcess, pThread, pEngine
                };
                try
                {
                    if (riidEvent == typeof(IDebugProcessInfoUpdatedEvent158).GUID)
                    {
                        ErrorHandler.ThrowOnFailure(
                        ((IDebugProcessInfoUpdatedEvent158)pEvent)
                            .GetUpdatedProcessInfo(out var str, out var pid));
                    }
                    else if (riidEvent == typeof(IDebugOutputStringEvent2).GUID)
                    {

                        ErrorHandler.ThrowOnFailure(
                        ((IDebugOutputStringEvent2)pEvent).GetString(out var str));
                        //if (this.console is not null)
                        //{
                        //    var cancellationToken = CancellationToken.None;
                        //    this.console.WriteAsync(str, cancellationToken)
                        //        .AsTask()
                        //        .FileAndForget("/LinuxDebugger/Write");
                        //}
                    }
                    else if (riidEvent == typeof(IDebugProgramCreateEvent2).GUID)
                    {
                        if (this.Profile?.LaunchBrowser == true)
                        {
                            this.threadingService
                                .JoinableTaskFactory
                                .RunAsync(VsTaskRunContext.UIThreadIdlePriority, async () =>
                                {
                                    await Task.Delay(1000).ConfigureAwait(false);
                                    await this.launchBrowserAsync().ConfigureAwait(false);
                                })
                                .FileAndForget();
                        }
                    }
                }
                finally
                {
                    foreach (object o in objectList)
                    {
                        if (o != null && Marshal.IsComObject(o))
                            Marshal.ReleaseComObject(o);
                    }
                }
                return VSConstants.S_OK;
            }
        }
    }
}
