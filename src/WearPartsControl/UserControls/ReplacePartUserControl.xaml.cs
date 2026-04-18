using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ReplacePartUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ReplacePartUserControl : UserControl
    {
        public ReplacePartUserControl(ReplacePartViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ReplacePartViewModel viewModel)
            {
                await viewModel.InitializeAsync().ConfigureAwait(true);
            }
        }
    }
}
