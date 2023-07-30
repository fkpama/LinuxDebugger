using System.Windows;
using System.Windows.Controls;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem.Controls
{
    /// <summary>
    /// Interaction logic for ConnectionSelectionWindow.xaml
    /// </summary>
    public partial class ConnectionSelectionWindow : DialogWindow
    {
        public ConnectionSelectionWindow()
        {
            InitializeComponent();
        }

        private void OnWindowClose(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
