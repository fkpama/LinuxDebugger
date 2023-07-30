using System.Windows.Controls;
using RemoteCSharp.ViewModels;

namespace RemoteCSharp.Controls
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
