#nullable disable
using System.Diagnostics;
using System.Runtime.InteropServices;
using EnvDTE;
using liblinux.Services;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Linux.Package;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Terminal;
using Nerdbank.Streams;

namespace LinuxDebugger.VisualStudio.Terminal;

internal sealed class LinuxConsolePtyProxy
    : IDebugEventCallback2,
    IVsDebuggerEvents,
    IVsWindowFrameNotify3,
    IVsWindowFrameNotify,
    IDisposable
{
    public static readonly Guid TermWindowRendererGuid = new("E8034F19-AB72-4F06-83FD-F6832B41AA63");
    public static readonly Guid TermWindowGuid = new("D212F56B-C48A-434C-A121-1C5D80B59B9F");
    private static readonly byte[] s_clearScreenBytes = new byte[] { 27, 99 };
    private static readonly int[] s_findToolWinErrs = new[] { VSConstants.E_FAIL };
    private PtyDebugProxy thePty;
    private Stream remoteStream;
    private SshConnectionInfo connectionInfo;
    private readonly ITerminalService terminalService;
    private Guid terminalGuid = Guid.Empty;
    private readonly DTE dte;
    private uint pid, frameCookie, cookie;
    private int rows, columns;
    private readonly BaseDebugFilter filter;
    private readonly Func<PtyDebugProxy> proxyFactory;
    private readonly JoinableTaskFactory taskFactory;
    private Stream ptyStream;
    private IVsWindowFrame2 frame;
    private bool? isShown;
    private readonly IVsDebugger debugger;
    private readonly IVsUIShell uiShell;

    public event EventHandler<TerminalClosedEventArgs> TerminalClosed;
    //private static readonly Guid EngineId = new Guid("{ea6637c6-17df-45b5-a183-0951c54243bc}");
    //private static readonly Guid GdbEngine = new Guid("{91744D97-430F-42C1-9779-A5813EBD6AB2}");
    //private static readonly Guid LldbEngine = new Guid("{5D630903-189D-4837-9785-699B05BEC2A9}");

    public string Tty => this.thePty.Filter.Tty;
    public event EventHandler<PtyExitedEventArgs> PtyExited;

    private LinuxConsolePtyProxy(
      ITerminalService terminalService,
      DTE dte,
      IVsDebugger debuggerService,
      IVsUIShell uiShell,
      JoinableTaskFactory taskFactory,
      SshConnectionInfo connectionInfo)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.terminalService = terminalService;
        this.dte = dte;
        this.taskFactory = taskFactory;
        this.debugger = debuggerService;
        this.uiShell = uiShell;
        this.connectionInfo = connectionInfo;
    }
    public LinuxConsolePtyProxy(
      ITerminalService terminalService,
      GdbServer server,
      DTE dte,
      IVsDebugger debuggerService,
      IVsUIShell shell,
      SshConnectionInfo connectionInfo,
      JoinableTaskFactory taskFactory)
        : this(terminalService,
               dte,
               debuggerService,
               shell,
               taskFactory,
               connectionInfo)
    {
        this.filter = new SSHGdbServerDebugFilter(this, server);
        this.proxyFactory = () => new(server, this.filter, this.columns, this.rows);
    }
    public LinuxConsolePtyProxy(
      ITerminalService terminalService,
      SshConnectionInfo connectionInfo,
      DTE dte,
      IVsUIShell shell,
      IVsDebugger debuggerService,
      JoinableTaskFactory taskFactory)
        : this(terminalService,
               dte,
               debuggerService,
               shell,
               taskFactory,
               connectionInfo)
    {
        this.filter = new SSHGdbDebugFilter(this);
        this.proxyFactory = () => new(connectionInfo.CreateRemoteSystem(), this.filter, this.columns, this.rows);
    }

    public async Task<string> ConnectAsync(
      CancellationToken token)
    {
        this.pid = 0U;
        if (this.CheckTtyExists())
        {
            // ISSUE: explicit non-virtual call
            await this.DisconnectAsync().ConfigureAwait(false);
        }
        //IRemoteSystem remoteSystemAsync = await factory.CreateRemoteSystemAsync(token);
        //var filter =  new SSHGdbDebugFilter(this);
        //this.thePty = new PtyDebugProxy(remoteSystemAsync, filter, this.columns, this.rows);
        this.thePty = this.proxyFactory();
        //if (getOutputFilter != null)
        //    filter.TextFilter = getOutputFilter(filter);
        await this.InitLinuxConsoleAsync(token).ConfigureAwait(false);
        await this.ConnectPtyAsync(token);
        await this.ClearScreenAsync(token);
        return this.thePty.Filter.Tty;
    }

    //public async Task ConnectAsync(
    //  GdbServer gdbserver,
    //  Func<TextFilter, TextFilter> getOutputFilter,
    //  CancellationToken token)
    //{
    //    LinuxConsolePtyProxy linuxConsole = this;
    //    linuxConsole.pid = 0U;
    //    if (linuxConsole.CheckTtyExists())
    //    {
    //        // ISSUE: explicit non-virtual call
    //        await linuxConsole.DisconnectAsync().ConfigureAwait(false);
    //    }
    //    BaseDebugFilter filter = new SSHGdbServerDebugFilter(linuxConsole, gdbserver);
    //    linuxConsole.thePty = new PtyDebugProxy(gdbserver, filter, linuxConsole.columns, linuxConsole.rows);
    //    if (getOutputFilter != null)
    //        filter.TextFilter = getOutputFilter(filter);
    //    await linuxConsole.ConnectPtyAsync(token);
    //    await linuxConsole.ClearScreen();
    //}

    public async Task SendCtrlCAsync()
    {
        if (!ThreadHelper.CheckAccess())
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        try
        {
            var num = this.pid;
            if (num != 0U && this.dte.Debugger.DebuggedProcesses.Count > 0)
                num = (uint)this.dte.Debugger.DebuggedProcesses.Item(1).Programs.Item(1).Threads.Item(1).ID;
            if (num != 0U)
                _ = this.thePty.System.Shell.ExecuteCommand(string.Format("kill -2 {0}", num), new TimeSpan?(TimeSpan.FromSeconds(10.0)));
        }
        catch
        {
        }
    }

    public async Task DisconnectAsync()
    {
        if (this.CheckTtyExists())
            await this.thePty.ClosePtyAsync();
        this.thePty = null;
    }

    public async Task InitLinuxConsoleAsync(CancellationToken token)
    {
        if (this.CheckTerminalExists())
            return;
        (this.remoteStream, this.ptyStream) = FullDuplexStream.CreatePair(null);
        this.register();
        await this.ConnectPtyAsync(token).ConfigureAwait(false);
    }

    private void register()
    {
        this.terminalService.TerminalClosed += this.TerminalClosedHandler;
        this.terminalService.TerminalResized += this.TerminalResized;
    }
    private void unregister()
    {
        this.terminalService.TerminalClosed -= this.TerminalClosedHandler;
        this.terminalService.TerminalResized -= this.TerminalResized;
    }

    private async Task ensureTerminalAsync(CancellationToken token)
    {
        await this.taskFactory.SwitchToMainThreadAsync(token);
        var terminalRendererAsync = await this.terminalService
            .CreateTerminalRendererAsync(this.ptyStream, token, DialogStrings.LinuxConsoleTitle);
        this.terminalGuid = terminalRendererAsync;
        await this.terminalService
                .EnableDebugModeSwitchPersistanceAsync(this.terminalGuid, token);
        (this.columns, this.rows) = await this.terminalService
                .GetSizeAsync(this.terminalGuid, token);
    }

    private void TerminalClosedHandler(object sender, TerminalClosedEventArgs e)
    {
        if (!(e.TerminalGuid == this.terminalGuid))
            return;
        this.unsubscribeEvents();
        this.unsubscribeWindowFrameAsync(VsShellUtilities.ShutdownToken)
            .AsTask()
            .FileAndForget("/ok");
        this.terminalGuid = Guid.Empty;
        this.remoteStream = null;
        this.ptyStream = null;
        this.unregister();
        if (this.CheckTtyExists())
        {
            this.thePty.Disconnect();
            this.thePty = null;
        }
        this.TerminalClosed?.Invoke(this, e);
    }

    private void unsubscribeEvents()
    {
        if (this.cookie > 0)
        {
            this.unsubcribeAsync(VsShellUtilities.ShutdownToken)
                .AsTask()
                .FileAndForget("/Linux");
        }
    }

    private ValueTask subscribeAsync(CancellationToken cancellationToken)
    {

        if (this.checkSubscribed())
        {
            return default;
        }
        return new(Task.Run(async () =>
        {
            await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);

            _ = ErrorHandler.ThrowOnFailure(
            this.debugger.AdviseDebuggerEvents(this, out var cookie));
            _ = ErrorHandler.ThrowOnFailure(
            this.debugger.AdviseDebugEventCallback(this));
            this.cookie = cookie;

        }, cancellationToken));
    }
    private ValueTask unsubcribeAsync(CancellationToken cancellationToken)
    {
        if (this.cookie <= 0)
            return default;
        return new(Task.Run(async () =>
        {
            if (this.cookie <= 0)
                return;
            await this.taskFactory.SwitchToMainThreadAsync();
            _ = ErrorHandler.ThrowOnFailure(this.debugger
                .UnadviseDebugEventCallback(this));
            _ = ErrorHandler.ThrowOnFailure(this.debugger
                .UnadviseDebuggerEvents(this.cookie));
            this.cookie = 0;
        }, cancellationToken));
    }

    private void TerminalResized(object sender, TerminalResizeEventArgs e)
    {
        if (!(e.TerminalGuid == this.terminalGuid))
            return;
        this.rows = e.MaxRows;
        this.columns = e.MaxColumns;
        this.thePty?.ResizePty(this.columns, this.rows);
    }

    private bool CheckTerminalExists() => this.terminalGuid != Guid.Empty;
    private bool checkSubscribed() => this.cookie > 0;

    private bool CheckTtyExists() => this.thePty != null;

    private async Task ConnectPtyAsync(CancellationToken token)
    {
        if (this.CheckTtyExists())
            await this.thePty.InitPtyAsync(this.remoteStream);
        if (!this.CheckTerminalExists())
            return;
        if (this.columns > 0)
            this.thePty?.ResizePty(this.columns, this.rows);
        //await this.terminalService.ShowAsync(this.terminalGuid, token);
    }

    public async Task ClearScreenAsync(CancellationToken cancellationToken)
    {
        if (!this.CheckTerminalExists() || this.remoteStream is null)
            return;
        await this.remoteStream.WriteAsync(s_clearScreenBytes, 0, 2, cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task ShowAsync(CancellationToken cancellationToken)
    {
        await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (!this.CheckTerminalExists())
            await this.ensureTerminalAsync(cancellationToken);

        await this.terminalService.ShowAsync(this.terminalGuid, cancellationToken);

        if (this.frame is null)
        {
            Debug.Assert(this.frameCookie == 0);
            this.frame = await this.findFrameAsync(cancellationToken);
            _ = ErrorHandler.ThrowOnFailure(
            this.frame.Advise(this, out this.frameCookie));
        }
        else if (this.frameCookie == 0)
        {
            _ = ErrorHandler.ThrowOnFailure(
            this.frame.Advise(this, out this.frameCookie));
        }
        var shown = Convert.ToBoolean(((IVsWindowFrame)this.frame).IsVisible());
        this.isShown = shown;
        if (!shown)
        {
            _ = ErrorHandler.ThrowOnFailure(((IVsWindowFrame)this.frame).Show());
        }
    }

    private ValueTask<IVsWindowFrame2> findFrameAsync(CancellationToken cancellationToken)
    {
        if (this.frame is not null)
            return new(this.frame);
        return new(Task.Run(async () =>
        {
            if (!this.taskFactory.Context.IsOnMainThread)
                await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
            var guid = TermWindowRendererGuid;
            if (ErrorHandler.Failed(ErrorHandler.ThrowOnFailure(
            this.uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst,
                                        ref guid,
                                        out var frame), s_findToolWinErrs)))
            {
                guid = TermWindowGuid;
                if (ErrorHandler.Failed(ErrorHandler.ThrowOnFailure(
                this.uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst,
                                            ref guid,
                                            out frame), s_findToolWinErrs)))
                {
                    _ = ErrorHandler.ThrowOnFailure(
                        this.uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst,
                                                ref this.terminalGuid,
                                                out frame), s_findToolWinErrs);
                }
            }

            return (IVsWindowFrame2)frame;
        }, cancellationToken));
    }

    private ValueTask unsubscribeWindowFrameAsync(CancellationToken cancellationToken)
    {
        if (!this.taskFactory.Context.IsOnMainThread)
        {
            return new(Task.Run(async () =>
            {
                await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
                this.unsubscribeWindowFrame();
            }, cancellationToken));
        }
        this.unsubscribeWindowFrame();
        return default;
    }

    private void unsubscribeWindowFrame()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.isShown = null;
        if (this.frameCookie != 0)
        {
            Assumes.NotNull(this.frame);
            _ = ErrorHandler.ThrowOnFailure(
            this.frame.Unadvise(this.frameCookie));
            this.frameCookie = 0;
        }

        if (this.frame is not null)
        {
            if (Marshal.IsComObject(this.frame))
                _ = Marshal.ReleaseComObject(this.frame);
            this.frame = null;
        }
    }

    internal ValueTask ResetAsync(SshConnectionInfo connectionInfo, CancellationToken cancellationToken)
    {
        if (this.remoteStream is not null
            && this.connectionInfo?.InternalId == connectionInfo.InternalId)
        {
            if (!this.checkSubscribed())
            {
                return new(Task.Run(() => this.subscribeAsync(cancellationToken).AsTask()));
            }
            return default;
        }

        return new(Task.Run(async () =>
        {
            this.connectionInfo = connectionInfo;
            await this.subscribeAsync(cancellationToken).ConfigureAwait(false);
            _ = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken));
    }

    public void Dispose() => ThreadHelper.JoinableTaskFactory.Run(async () =>
    {
        if (this.CheckTtyExists())
            await this.DisconnectAsync().ConfigureAwait(false);
        var cancellationToken = VsShellUtilities.ShutdownToken;
        if (this.CheckTerminalExists())
        {
            await this.terminalService
            .CloseAsync(this.terminalGuid, cancellationToken)
            .ConfigureAwait(false);
        }

        if (this.frame is not null && !VsShellUtilities.ShellIsShuttingDown)
        {
            await this.taskFactory.SwitchToMainThreadAsync(cancellationToken);
            this.unsubscribeWindowFrame();
        }
    });

    #region IVsDebuggerEvents implementation

    int IVsDebuggerEvents.OnModeChange(DBGMODE dbgmodeNew)
    {
        if (dbgmodeNew == DBGMODE.DBGMODE_Run)
        {
            this.ShowAsync(CancellationToken.None)
               .FileAndForget("Show");
        }
        else if (dbgmodeNew == DBGMODE.DBGMODE_Design)
        {
            this.unsubcribeAsync(CancellationToken.None)
                .AsTask()
                .FileAndForget("forgetted");
        }
        return VSConstants.S_OK;
    }

    #endregion IVsDebuggerEvents implementation

    #region IDebugEventCallback2 implementation

    int IDebugEventCallback2.Event(IDebugEngine2 engine,
                                   IDebugProcess2 process,
                                   IDebugProgram2 program,
                                   IDebugThread2 thread,
                                   IDebugEvent2 debugEvent,
                                   ref Guid riidEvent,
                                   uint attrib)
    {
        var objectList = new List<object>()
        { engine, process, program, thread, debugEvent };

        try
        {
            if (riidEvent == typeof(IDebugProcessInfoUpdatedEvent158).GUID)
            {
                _ = ErrorHandler.ThrowOnFailure(
                ((IDebugProcessInfoUpdatedEvent158)debugEvent)
                    .GetUpdatedProcessInfo(out _, out var num));
                this.pid = num;
            }
            else if (riidEvent == typeof(IDebugOutputStringEvent2).GUID)
            {
                if (this.isShown.HasValue && !this.isShown.Value)
                {
                    this.ShowAsync(CancellationToken.None).FileAndForget();
                }
            }
        }
        finally
        {
            foreach (var o in objectList)
            {
                if (o != null && Marshal.IsComObject(o))
                    _ = Marshal.ReleaseComObject(o);
            }
        }
        return 0;
    }

    #endregion IDebugEventCallback2 implementation

    #region IVsWindowFrameNotify implementation

    int IVsWindowFrameNotify3.OnShow(int fShow)
    {
        var show = (__FRAMESHOW3)fShow;
        this.isShown = show == __FRAMESHOW3.FRAMESHOW_WinActivated;
        return VSConstants.S_OK;
    }

    int IVsWindowFrameNotify3.OnMove(int x, int y, int w, int h)
        => VSConstants.S_OK;

    int IVsWindowFrameNotify3.OnSize(int x, int y, int w, int h)
        => VSConstants.S_OK;

    int IVsWindowFrameNotify3.OnDockableChange(int fDockable, int x, int y, int w, int h)
        => VSConstants.S_OK;

    int IVsWindowFrameNotify3.OnClose(ref uint pgrfSaveOptions)
        => VSConstants.S_OK;

    int IVsWindowFrameNotify.OnShow(int fShow)
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
        => ((IVsWindowFrameNotify3)this).OnShow(fShow);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

    int IVsWindowFrameNotify.OnMove()
        => VSConstants.S_OK;

    int IVsWindowFrameNotify.OnSize()
        => VSConstants.S_OK;
    int IVsWindowFrameNotify.OnDockableChange(int fDockable)
        => VSConstants.S_OK;

    #endregion IVsWindowFrameNotify implementation
}
#nullable restore
