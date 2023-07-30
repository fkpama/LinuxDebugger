using System.Windows.Controls;
using LinuxDebugger.ViewModels;

namespace LinuxDebugger.Controls
{
    /// <summary>
    /// Interaction logic for OptionPageControl.xaml
    /// </summary>
    public partial class OptionPageControl : UserControl
    {
        public OptionPageControl()
            : this(new()) { }
        public OptionPageControl(OptionsViewModel viewModel)
        {
            this.DataContext = viewModel;
            InitializeComponent();
        }
    }
}
