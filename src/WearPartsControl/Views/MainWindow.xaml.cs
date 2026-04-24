using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ViewModels;

namespace WearPartsControl.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;
        private bool _isClosingIntercepted;

        public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider, IAutoLogoutInteractionService autoLogoutInteractionService)
        {
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            _autoLogoutInteractionService = autoLogoutInteractionService;
            DataContext = viewModel;
            InitializeComponent();

            Closing += OnMainWindowClosing;
            PreviewMouseDown += OnUserInteraction;
            PreviewKeyDown += OnUserKeyInteraction;
            PreviewGotKeyboardFocus += OnUserInteraction;
            _viewModel.LoginRequested += OnLoginRequested;
            Closed += OnMainWindowClosed;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return _viewModel.InitializeAsync(cancellationToken);
        }

        private void OnLoginRequested(object? sender, EventArgs e)
        {
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Owner = this;
            loginWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _autoLogoutInteractionService.RunModal(() => loginWindow.ShowDialog());
        }

        private void OnUserInteraction(object? sender, RoutedEventArgs e)
        {
            _autoLogoutInteractionService.NotifyActivity();
        }

        private void OnUserKeyInteraction(object? sender, KeyEventArgs e)
        {
            _autoLogoutInteractionService.NotifyActivity();
        }

        private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
        {
            if (Application.Current is not App app)
            {
                return;
            }

            if (app.IsShutdownRequested || _isClosingIntercepted)
            {
                return;
            }

            e.Cancel = true;
            _isClosingIntercepted = true;

            try
            {
                await app.RequestShutdownAsync("用户关闭主窗口").ConfigureAwait(true);
            }
            finally
            {
                _isClosingIntercepted = false;
            }
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            Closing -= OnMainWindowClosing;
            PreviewMouseDown -= OnUserInteraction;
            PreviewKeyDown -= OnUserKeyInteraction;
            PreviewGotKeyboardFocus -= OnUserInteraction;
            _viewModel.LoginRequested -= OnLoginRequested;
            Closed -= OnMainWindowClosed;
        }
    }
}
