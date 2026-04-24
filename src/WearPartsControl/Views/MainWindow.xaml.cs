using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.Localization;
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
        private bool _isExitRequested;
        private bool _isInTray;

        public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider, IAutoLogoutInteractionService autoLogoutInteractionService)
        {
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            _autoLogoutInteractionService = autoLogoutInteractionService;
            RestoreFromTrayCommand = new RelayCommand(RestoreFromTray);
            ExitFromTrayCommand = new AsyncRelayCommand(ExitFromTrayAsync);
            DataContext = viewModel;
            InitializeComponent();

            TrayContextContentControl.DataContext = viewModel;
            TrayContextContentControl.RestoreCommand = RestoreFromTrayCommand;
            TrayContextContentControl.ExitCommand = ExitFromTrayCommand;

            Closing += OnMainWindowClosing;
            StateChanged += OnMainWindowStateChanged;
            Activated += OnMainWindowActivated;
            PreviewMouseDown += OnUserInteraction;
            PreviewKeyDown += OnUserKeyInteraction;
            PreviewGotKeyboardFocus += OnUserInteraction;
            _viewModel.LoginRequested += OnLoginRequested;
            Closed += OnMainWindowClosed;
        }

        public IRelayCommand RestoreFromTrayCommand { get; }

        public IAsyncRelayCommand ExitFromTrayCommand { get; }

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

            if (app.IsShutdownRequested || _isClosingIntercepted || _isExitRequested)
            {
                return;
            }

            e.Cancel = true;

            var result = _autoLogoutInteractionService.RunModal(() => System.Windows.MessageBox.Show(
                this,
                LocalizedText.Get("MainWindowTrayContent.ClosePromptMessage"),
                LocalizedText.Get("MainWindowTrayContent.ClosePromptTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes));

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                SendToTray();
                return;
            }

            _isClosingIntercepted = true;
            try
            {
                await app.RequestShutdownAsync("用户关闭主窗口并退出程序").ConfigureAwait(true);
            }
            finally
            {
                _isClosingIntercepted = false;
            }
        }

        private void OnMainWindowStateChanged(object? sender, EventArgs e)
        {
            if (_isExitRequested || (Application.Current is App app && app.IsShutdownRequested))
            {
                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                SendToTray();
                return;
            }

            if (!_isInTray)
            {
                HideTrayIcon();
            }
        }

        private void OnMainWindowActivated(object? sender, EventArgs e)
        {
            if (!_isInTray)
            {
                HideTrayIcon();
            }
        }

        private void OnTrayNotifyIconMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void SendToTray()
        {
            if (_isInTray)
            {
                return;
            }

            _isInTray = true;
            TrayNotifyIcon.Visibility = Visibility.Visible;
            ShowInTaskbar = false;
            Hide();
        }

        private void RestoreFromTray()
        {
            _isInTray = false;
            ShowInTaskbar = true;

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Show();
            Activate();
            HideTrayIcon();
        }

        private async Task ExitFromTrayAsync()
        {
            if (Application.Current is not App app)
            {
                _isExitRequested = true;
                Close();
                return;
            }

            _isExitRequested = true;
            await app.RequestShutdownAsync("用户从托盘退出程序").ConfigureAwait(true);
        }

        private void HideTrayIcon()
        {
            TrayNotifyIcon.Visibility = Visibility.Collapsed;
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            Closing -= OnMainWindowClosing;
            StateChanged -= OnMainWindowStateChanged;
            Activated -= OnMainWindowActivated;
            PreviewMouseDown -= OnUserInteraction;
            PreviewKeyDown -= OnUserKeyInteraction;
            PreviewGotKeyboardFocus -= OnUserInteraction;
            _viewModel.LoginRequested -= OnLoginRequested;
            Closed -= OnMainWindowClosed;
        }
    }
}
