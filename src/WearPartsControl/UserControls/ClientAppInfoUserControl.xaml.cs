using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ClientAppInfoUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ClientAppInfoUserControl : UserControl
    {
        private readonly ClientAppInfoViewModel _viewModel;
        private bool _isInitialized;

        public ClientAppInfoUserControl(ClientAppInfoViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
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
            await _viewModel.InitializeAsync().ConfigureAwait(true);
        }
    }
}
