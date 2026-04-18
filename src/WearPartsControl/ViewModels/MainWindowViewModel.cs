using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.UserControls;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        private static readonly object PlaceholderContent = new();

        private string? _selectedTabHeader;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly ILoginService _loginService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IUiBusyService _uiBusyService;
        private readonly IPlcStartupConnectionService _plcStartupConnectionService;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly IReadOnlyList<string> _allTabs;
        private string _currentUserWorkIdText = "工号：--";
        private string _currentUserAccessLevelText = "权限：--";
        private IEnumerable<string> _tabs = Array.Empty<string>();
        private bool _isLoggedIn;
        private bool _isClientAppInfoConfigured;
        private int _autoLogoutCountdownSeconds = 360;
        private int _remainingAutoLogoutSeconds;
        private CancellationTokenSource? _autoLogoutCancellationTokenSource;
        private int _initializeStarted;
        private bool _defaultContentPending;

        public MainWindowViewModel(
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            ICurrentUserAccessor currentUserAccessor,
            ILoginService loginService,
            IAppSettingsService appSettingsService,
            IUiBusyService uiBusyService,
            IPlcStartupConnectionService plcStartupConnectionService,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        {
            Title = localizationService["MainWindow.Title"];
            TabChangedCommand = new RelayCommand<int>(OnTabChanged);
            OpenLoginCommand = new RelayCommand(OnOpenLoginRequested);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync, CanLogout);
            _serviceProvider = serviceProvider;
            _currentUserAccessor = currentUserAccessor;
            _loginService = loginService;
            _uiBusyService = uiBusyService;
            _plcStartupConnectionService = plcStartupConnectionService;
            _delayAsync = delayAsync ?? Task.Delay;
            _selectedContent = PlaceholderContent;
            _appSettingsService = appSettingsService;
            _allTabs = localizationService.Catalog.MainWindow.Tabs.ToArray();

            SoftwareVersionText = $"软件版本：{ResolveVersion()}";
            _currentUserAccessor.CurrentUserChanged += OnCurrentUserChanged;
            _appSettingsService.SettingsSaved += OnAppSettingsSaved;
            _uiBusyService.PropertyChanged += OnUiBusyServicePropertyChanged;
            UpdateCurrentUserState();

            var appSettings = _appSettingsService.GetAsync(default).GetAwaiter().GetResult();
            ApplyAutoLogoutSettings(appSettings);
            ApplyClientAppInfoState(appSettings.IsSetClientAppInfo);
        }

        public event EventHandler? LoginRequested;

        public string Title { get; set; }

        public string CurrentUserWorkIdText
        {
            get => _currentUserWorkIdText;
            private set => SetProperty(ref _currentUserWorkIdText, value);
        }

        public string CurrentUserAccessLevelText
        {
            get => _currentUserAccessLevelText;
            private set => SetProperty(ref _currentUserAccessLevelText, value);
        }

        public string SoftwareVersionText { get; }

        public bool IsBusy => _uiBusyService.IsBusy;

        public bool IsNotBusy => !IsBusy;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            private set
            {
                if (SetProperty(ref _isLoggedIn, value))
                {
                    OnPropertyChanged(nameof(ShowLoginButton));
                    OnPropertyChanged(nameof(ShowLogoutButton));
                    LogoutCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsClientAppInfoConfigured
        {
            get => _isClientAppInfoConfigured;
            private set => SetProperty(ref _isClientAppInfoConfigured, value);
        }

        public bool ShowLoginButton => !IsLoggedIn;

        public bool ShowLogoutButton => IsLoggedIn;

        private object _selectedContent;

        public object SelectedContent
        {
            get { return _selectedContent; }
            set => SetProperty(ref _selectedContent, value);
        }

        public IEnumerable<string> Tabs
        {
            get => _tabs;
            private set => SetProperty(ref _tabs, value);
        }

        public string? SelectedTabHeader
        {
            get => _selectedTabHeader;
            private set
            {
                if (_selectedTabHeader == value)
                {
                    return;
                }

                _selectedTabHeader = value;
                OnPropertyChanged();
            }
        }

        public ICommand TabChangedCommand { get; }

        public ICommand OpenLoginCommand { get; }

        public IAsyncRelayCommand LogoutCommand { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _initializeStarted, 1) == 1)
            {
                return;
            }

            await Task.Yield();
            EnsureDefaultContentLoaded();

            if (!IsClientAppInfoConfigured)
            {
                await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            using (_uiBusyService.Enter())
            {
                await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private bool CanLogout() => IsLoggedIn;

        private void OnTabChanged(int index)
        {
            if (!_isClientAppInfoConfigured)
            {
                SelectedContent = _serviceProvider.GetRequiredService<ClientAppInfoUserControl>();
                return;
            }

            switch (index)
            {
                case 1:
                    SelectedContent = _serviceProvider.GetRequiredService<ClientAppInfoUserControl>();
                    break;
                case 2:
                    SelectedContent = _serviceProvider.GetRequiredService<PartManagementUserControl>();
                    break;
                case 3:
                    SelectedContent = _serviceProvider.GetRequiredService<PartUpdateRecordUserControl>();
                    break;
                case 4:
                    SelectedContent = _serviceProvider.GetRequiredService<UserConfigUserControl>();
                    break;
                default:
                    SelectedContent = _serviceProvider.GetRequiredService<ReplacePartUserControl>();
                    break;
            }
        }

        private void OnOpenLoginRequested()
        {
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAppSettingsSaved(object? sender, AppSettings settings)
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    ApplyAutoLogoutSettings(settings);
                    ApplyClientAppInfoState(settings.IsSetClientAppInfo);
                });
                return;
            }

            ApplyAutoLogoutSettings(settings);
            ApplyClientAppInfoState(settings.IsSetClientAppInfo);
        }

        private async Task LogoutAsync()
        {
            await _loginService.LogoutAsync().ConfigureAwait(false);
        }

        private void OnCurrentUserChanged(object? sender, EventArgs e)
        {
            RunOnUiThread(UpdateCurrentUserState);
        }

        private void OnUiBusyServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IUiBusyService.IsBusy))
            {
                RunOnUiThread(() =>
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(IsNotBusy));
                    LogoutCommand.NotifyCanExecuteChanged();
                });
            }
        }

        private void UpdateCurrentUserState()
        {
            var currentUser = _currentUserAccessor.CurrentUser;
            IsLoggedIn = currentUser is not null;
            CurrentUserWorkIdText = currentUser is null ? "工号：--" : $"工号：{currentUser.WorkId}";

            if (currentUser is null)
            {
                StopAutoLogoutCountdown();
                CurrentUserAccessLevelText = "权限：--";
                return;
            }

            StartAutoLogoutCountdown(currentUser);
        }

        private void ApplyAutoLogoutSettings(AppSettings settings)
        {
            _autoLogoutCountdownSeconds = settings.AutoLogoutCountdownSeconds <= 0
                ? 360
                : settings.AutoLogoutCountdownSeconds;

            if (_currentUserAccessor.CurrentUser is { } currentUser)
            {
                StartAutoLogoutCountdown(currentUser);
            }
        }

        private void StartAutoLogoutCountdown(MhrUser currentUser)
        {
            StopAutoLogoutCountdown();

            _remainingAutoLogoutSeconds = _autoLogoutCountdownSeconds;
            CurrentUserAccessLevelText = FormatAccessLevelText(currentUser.AccessLevel, _remainingAutoLogoutSeconds);

            var cts = new CancellationTokenSource();
            _autoLogoutCancellationTokenSource = cts;
            _ = RunAutoLogoutCountdownAsync(currentUser, cts.Token);
        }

        private async Task RunAutoLogoutCountdownAsync(MhrUser currentUser, CancellationToken cancellationToken)
        {
            try
            {
                while (_remainingAutoLogoutSeconds > 0)
                {
                    await _delayAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    _remainingAutoLogoutSeconds--;
                    RunOnUiThread(() =>
                    {
                        if (_currentUserAccessor.CurrentUser is not null)
                        {
                            CurrentUserAccessLevelText = FormatAccessLevelText(currentUser.AccessLevel, _remainingAutoLogoutSeconds);
                        }
                    });
                }

                cancellationToken.ThrowIfCancellationRequested();
                await _loginService.LogoutAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void StopAutoLogoutCountdown()
        {
            var cts = _autoLogoutCancellationTokenSource;
            _autoLogoutCancellationTokenSource = null;
            if (cts is not null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _remainingAutoLogoutSeconds = 0;
        }

        private static string FormatAccessLevelText(int accessLevel, int remainingSeconds)
        {
            var clampedSeconds = Math.Max(remainingSeconds, 0);
            var remaining = TimeSpan.FromSeconds(clampedSeconds);
            return $"权限：{accessLevel} | 自动注销：{remaining:mm\\:ss}";
        }

        private void ApplyClientAppInfoState(bool isConfigured)
        {
            IsClientAppInfoConfigured = isConfigured;

            if (isConfigured)
            {
                Tabs = _allTabs.ToArray();
                _defaultContentPending = true;
                if (Volatile.Read(ref _initializeStarted) == 1)
                {
                    EnsureDefaultContentLoaded();
                }

                return;
            }

            _defaultContentPending = false;
            Tabs = _allTabs.Count > 1
                ? new[] { _allTabs[1] }
                : _allTabs.ToArray();
            SelectedContent = _serviceProvider.GetRequiredService<ClientAppInfoUserControl>();
        }

        private void EnsureDefaultContentLoaded()
        {
            if (!_defaultContentPending)
            {
                return;
            }

            SelectedContent = _serviceProvider.GetRequiredService<ReplacePartUserControl>();
            _defaultContentPending = false;
        }

        private static string ResolveVersion()
        {
            var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
            if (version is null)
            {
                return "--";
            }

            return version.Build >= 0
                ? version.ToString(3)
                : version.ToString();
        }

        private static void RunOnUiThread(Action action)
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(action);
                return;
            }

            action();
        }
    }
}