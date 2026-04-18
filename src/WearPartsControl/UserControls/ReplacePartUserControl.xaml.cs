using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ReplacePartUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ReplacePartUserControl : UserControl
    {
        private bool _isInitialized;

        public ReplacePartUserControl(ReplacePartViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            if (DataContext is ReplacePartViewModel viewModel)
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                await viewModel.InitializeAsync().ConfigureAwait(true);
            }
        }
    }
}
