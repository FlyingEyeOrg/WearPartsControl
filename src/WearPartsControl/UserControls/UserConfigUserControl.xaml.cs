using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// UserConfigUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserConfigUserControl : UserControl
    {
        private readonly UserConfigViewModel _viewModel;
        private bool _isInitialized;

        public UserConfigUserControl(UserConfigViewModel viewModel)
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
