using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace LinuxDebugger.ProjectSystem.PropertyPages
{
    [Export(typeof(ILinkActionHandler))]
    [ExportMetadata("CommandName", "SshConnectionConfigurationCommand")]
    internal sealed class SshConfigurationActionHandler : ILinkActionHandler
    {
        private readonly IAsyncServiceProvider services;

        [ImportingConstructor]
        public SshConfigurationActionHandler(
            [Import(typeof(SVsServiceProvider))] IServiceProvider services)
        {
            this.services = services as IAsyncServiceProvider ?? AsyncServiceProvider.GlobalProvider;
        }
        public async Task HandleAsync(UnconfiguredProject project, IReadOnlyDictionary<string, string> editorMetadata)
        {
            var cancellationToken = VsShellUtilities.ShutdownToken;
            await showOptionPageAsync(project, cancellationToken).ConfigureAwait(false);
        }
        private async Task showOptionPageAsync(UnconfiguredProject project, CancellationToken cancellationToken)
        {
            var uiShell = await this.services
                .GetServiceAsync<SVsUIShell, IVsUIShell>()
                .ConfigureAwait(false);
            if (uiShell != null)
            {
                var dte = await this.services
                .GetServiceAsync<SDTE, DTE2>()
                .ConfigureAwait(false);
                var guid = VsMenus.guidStandardCommandSet97;
                var cmdId = (uint)VSConstants.VSStd97CmdID.ToolsOptions;
                object str = Constants.ConnectionManagerOptionPageGuid;
                await project.Services.ThreadingPolicy.SwitchToUIThread();
                ErrorHandler.ThrowOnFailure(uiShell
                    .PostExecCommand(ref guid, cmdId, 0, ref str));
            }
        }
    }
}
