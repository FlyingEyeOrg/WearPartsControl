using System.Windows.Controls;
using System.Windows;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;

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

        private async void OnPreviewComNotificationClicked(object sender, RoutedEventArgs e)
        {
            var preview = await _viewModel.BuildComNotificationPreviewAsync().ConfigureAwait(true);
            var dialog = new NotificationPreviewWindow(preview.Warning.Markdown, preview.Shutdown.Markdown);
            var owner = Window.GetWindow(this);
            if (owner is not null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();
        }
    }
}
