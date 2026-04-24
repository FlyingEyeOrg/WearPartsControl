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
        private bool _hasShownFirstTrayBalloonTip;

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

            var result = ShowClosePrompt();

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                SendToTray(hideFromTaskbar: true, showFirstBalloonTip: true);
                return;
            }

            if (!EnsureUserCanExit())
            {
                return;
            }

            await RequestApplicationShutdownAsync(app, "用户关闭主窗口并退出程序").ConfigureAwait(true);
        }

        private void OnMainWindowStateChanged(object? sender, EventArgs e)
        {
            if (_isExitRequested || (Application.Current is App app && app.IsShutdownRequested))
            {
                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                if (!_isInTray)
                {
                    HideTrayIcon();
                }

                return;
            }

            if (_isInTray && IsVisible)
            {
                _isInTray = false;
                HideTrayIcon();
            }
        }

        private void OnMainWindowActivated(object? sender, EventArgs e)
        {
            if (WindowState != WindowState.Minimized)
            {
                _isInTray = false;
                HideTrayIcon();
            }
        }

        private void OnTrayNotifyIconMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void SendToTray(bool hideFromTaskbar, bool showFirstBalloonTip)
        {
            _isInTray = true;
            TrayNotifyIcon.Visibility = Visibility.Visible;

            if (hideFromTaskbar)
            {
                ShowInTaskbar = false;
                if (IsVisible)
                {
                    Hide();
                }
            }
            else
            {
                ShowInTaskbar = true;
                if (!IsVisible)
                {
                    Show();
                }
            }

            ShowFirstTrayBalloonTipIfNeeded(showFirstBalloonTip);
        }

        private void RestoreFromTray()
        {
            _isInTray = false;
            ShowInTaskbar = true;

            if (!IsVisible)
            {
                Show();
            }

            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            HideTrayIcon();
        }

        private async Task ExitFromTrayAsync()
        {
            if (!EnsureUserCanExit())
            {
                return;
            }

            if (!ConfirmTrayExit())
            {
                return;
            }

            if (Application.Current is not App app)
            {
                _isExitRequested = true;
                Close();
                return;
            }

            await RequestApplicationShutdownAsync(app, "用户从托盘退出程序").ConfigureAwait(true);
        }

        private bool EnsureUserCanExit()
        {
            if (_viewModel.IsLoggedIn)
            {
                return true;
            }

            _autoLogoutInteractionService.RunModal(() => System.Windows.MessageBox.Show(
                this,
                LocalizedText.Get("MainWindowTrayContent.LoginRequiredToExitMessage"),
                LocalizedText.Get("MainWindowTrayContent.LoginRequiredToExitTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                MessageBoxResult.OK));
            return false;
        }

        private MessageBoxResult ShowClosePrompt()
        {
            return _autoLogoutInteractionService.RunModal(() => System.Windows.MessageBox.Show(
                this,
                LocalizedText.Get("MainWindowTrayContent.ClosePromptMessage"),
                LocalizedText.Get("MainWindowTrayContent.ClosePromptTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes));
        }

        private bool ConfirmTrayExit()
        {
            var result = _autoLogoutInteractionService.RunModal(() => System.Windows.MessageBox.Show(
                this,
                LocalizedText.Get("MainWindowTrayContent.ExitConfirmMessage"),
                LocalizedText.Get("MainWindowTrayContent.ExitConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No));

            return result == MessageBoxResult.Yes;
        }

        private void ShowFirstTrayBalloonTipIfNeeded(bool showFirstBalloonTip)
        {
            if (!showFirstBalloonTip || _hasShownFirstTrayBalloonTip)
            {
                return;
            }

            _hasShownFirstTrayBalloonTip = true;
            TrayNotifyIcon.ShowBalloonTip(
                LocalizedText.Get("MainWindowTrayContent.FirstMinimizeBalloonTitle"),
                LocalizedText.Get("MainWindowTrayContent.FirstMinimizeBalloonMessage"),
                HandyControl.Data.NotifyIconInfoType.Info);
        }

        private async Task RequestApplicationShutdownAsync(App app, string reason)
        {
            _isClosingIntercepted = true;
            _isExitRequested = true;

            try
            {
                await app.RequestShutdownAsync(reason).ConfigureAwait(true);
            }
            finally
            {
                _isExitRequested = app.IsShutdownRequested;
                _isClosingIntercepted = false;
            }
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
