using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.Dialogs;
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
        private readonly IAppDialogService _dialogService;
        private readonly Func<bool> _showLoginDialog;
        private bool _isClosingIntercepted;
        private bool _isExitRequested;
        private bool _isInTray;
        private bool _hasShownFirstTrayBalloonTip;
        private WindowState _windowStateBeforeTray = WindowState.Normal;
        private Rect _windowBoundsBeforeTray = Rect.Empty;

        public MainWindow(
            MainWindowViewModel viewModel,
            IServiceProvider serviceProvider,
            IAutoLogoutInteractionService autoLogoutInteractionService,
            Func<bool>? showLoginDialog = null,
            IAppDialogService? dialogService = null)
        {
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            _autoLogoutInteractionService = autoLogoutInteractionService;
            _dialogService = dialogService ?? new AppDialogService(autoLogoutInteractionService);
            _showLoginDialog = showLoginDialog ?? ShowLoginDialog;
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
            _ = _showLoginDialog();
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
            if (_isInTray || !IsVisible || WindowState == WindowState.Minimized)
            {
                return;
            }

            HideTrayIcon();
        }

        private void OnTrayNotifyIconMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void SendToTray(bool hideFromTaskbar, bool showFirstBalloonTip)
        {
            CaptureWindowPlacementForTray();
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

            RestoreWindowPlacementFromTray();
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            HideTrayIcon();
        }

        private async Task ExitFromTrayAsync()
        {
            if (!_viewModel.IsLoggedIn)
            {
                if (!PromptLoginForTrayExit())
                {
                    return;
                }

                if (Application.Current is not App loginApp)
                {
                    _isExitRequested = true;
                    Close();
                    return;
                }

                await RequestApplicationShutdownAsync(loginApp, "用户登录后从托盘退出程序").ConfigureAwait(true);
                return;
            }

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

        private bool PromptLoginForTrayExit()
        {
            return _showLoginDialog();
        }

        private bool EnsureUserCanExit()
        {
            if (_viewModel.IsLoggedIn)
            {
                return true;
            }

            _dialogService.ShowMessage(
                LocalizedText.Get("MainWindowTrayContent.LoginRequiredToExitMessage"),
                LocalizedText.Get("MainWindowTrayContent.LoginRequiredToExitTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                this,
                MessageBoxResult.OK);
            return false;
        }

        private MessageBoxResult ShowClosePrompt()
        {
            return _dialogService.ShowMessage(
                LocalizedText.Get("MainWindowTrayContent.ClosePromptMessage"),
                LocalizedText.Get("MainWindowTrayContent.ClosePromptTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                this,
                MessageBoxResult.Cancel);
        }

        private bool ConfirmTrayExit()
        {
            var result = _dialogService.ShowMessage(
                LocalizedText.Get("MainWindowTrayContent.ExitConfirmMessage"),
                LocalizedText.Get("MainWindowTrayContent.ExitConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                this,
                MessageBoxResult.No);

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

        private void CaptureWindowPlacementForTray()
        {
            var windowState = WindowState == WindowState.Minimized
                ? _windowStateBeforeTray
                : WindowState;

            _windowStateBeforeTray = windowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;

            var bounds = RestoreBounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                _windowBoundsBeforeTray = bounds;
                return;
            }

            _windowBoundsBeforeTray = new Rect(Left, Top, Width, Height);
        }

        private void RestoreWindowPlacementFromTray()
        {
            WindowState = WindowState.Normal;

            if (_windowBoundsBeforeTray.Width > 0 && _windowBoundsBeforeTray.Height > 0)
            {
                Left = _windowBoundsBeforeTray.Left;
                Top = _windowBoundsBeforeTray.Top;
                Width = _windowBoundsBeforeTray.Width;
                Height = _windowBoundsBeforeTray.Height;
            }

            WindowState = _windowStateBeforeTray == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
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

        private bool ShowLoginDialog()
        {
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            return _dialogService.ShowDialog(loginWindow, ResolveLoginDialogOwner());
        }

        private Window? ResolveLoginDialogOwner()
        {
            if (_isInTray || !IsVisible || WindowState == WindowState.Minimized)
            {
                return null;
            }

            return this;
        }

        private void HideTrayIcon()
        {
            TrayNotifyIcon.Visibility = Visibility.Collapsed;
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            _isInTray = false;
            HideTrayIcon();
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
