using System.Windows;
using LinuxDebugger.ProjectSystem;
using LinuxDebugger.VisualStudio.Settings;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using LinuxDebugger.Controls;
using LinuxDebugger.ViewModels;

namespace LinuxDebugger
{
    internal class RemoteDotNetOptionPage : UIElementDialogPage
    {
        private readonly OptionsViewModel viewModel = new();
        private readonly AsyncLazy<LinuxDebuggerSettings> settingsLay;
        private ILinuxDebuggerSettingsManager settingsManager;

        internal ILinuxDebuggerSettingsManager SettingsManager
        {
            get
            {
                if (this.settingsManager is null)
                {
                    var svc = (IComponentModel)GetService(typeof(SComponentModel));
                    this.settingsManager = svc.GetService<ILinuxDebuggerSettingsManager>();
                }
                return settingsManager;
            }
        }

        internal LinuxDebuggerSettings Settings
        {
            get => settingsLay.GetValue(VsShellUtilities.ShutdownToken);
        }

        protected override UIElement Child
        {
            get
            {
                return new OptionPageControl { DataContext = this.viewModel };
            }
        }

        public RemoteDotNetOptionPage()
        {
            this.settingsLay = new(() =>
            {
                return this.SettingsManager
                .GetSettingsAsync(VsShellUtilities.ShutdownToken).AsTask();
            }, ThreadHelper.JoinableTaskFactory);
        }

        public override object AutomationObject => this.viewModel;

        public override void LoadSettingsFromStorage()
        {
            this.viewModel.Initialize(this.Settings);
        }

        public override void SaveSettingsToStorage()
        {
            _ = this.SettingsManager
                .SaveAsync(VsShellUtilities.ShutdownToken);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            this.viewModel.Update(this.Settings);
            base.OnApply(e);
        }
    }
}