using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.UserControls;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        private static readonly object PlaceholderContent = new();

        private string? _selectedTabHeader;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoginService _loginService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IUiBusyService _uiBusyService;
        private readonly IPlcStartupConnectionService _plcStartupConnectionService;
        private readonly ILoginSessionStateMachine _loginSessionStateMachine;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IAppStartupCoordinator _appStartupCoordinator;
        private readonly IReadOnlyList<string> _allTabs;
        private string _currentUserWorkIdText = LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserWorkIdEmpty");
        private string _currentUserAccessLevelText = LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserAccessLevelEmpty");
        private IEnumerable<string> _tabs = Array.Empty<string>();
        private bool _isLoggedIn;
        private bool _isClientAppInfoConfigured;
        private int _selectedTabIndex;
        private int _initializeStarted;
        private bool _defaultContentPending;

        public MainWindowViewModel(
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            ILoginService loginService,
            IAppSettingsService appSettingsService,
            IUiBusyService uiBusyService,
            IPlcStartupConnectionService plcStartupConnectionService,
            ILoginSessionStateMachine loginSessionStateMachine,
            IUiDispatcher uiDispatcher,
            IAppStartupCoordinator appStartupCoordinator)
        {
            Title = localizationService["MainWindow.Title"];
            TabChangedCommand = new RelayCommand<int>(OnTabChanged);
            OpenLoginCommand = new RelayCommand(OnOpenLoginRequested);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync, CanLogout);
            _serviceProvider = serviceProvider;
            _loginService = loginService;
            _uiBusyService = uiBusyService;
            _plcStartupConnectionService = plcStartupConnectionService;
            _loginSessionStateMachine = loginSessionStateMachine;
            _uiDispatcher = uiDispatcher;
            _appStartupCoordinator = appStartupCoordinator;
            _selectedContent = PlaceholderContent;
            _appSettingsService = appSettingsService;
            _allTabs = localizationService.Catalog.MainWindow.Tabs.ToArray();

            SoftwareVersionText = LocalizedText.Format("ViewModels.MainWindowVm.SoftwareVersion", ResolveVersion());
            _loginSessionStateMachine.StateChanged += OnLoginSessionStateChanged;
            _appSettingsService.SettingsSaved += OnAppSettingsSaved;
            _uiBusyService.PropertyChanged += OnUiBusyServicePropertyChanged;
            ApplyLoginState(_loginSessionStateMachine.Current);
            ApplyClientAppInfoState(false);
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

        public string LoadingText => _uiBusyService.BusyMessage;

        public bool HasLoadingText => !string.IsNullOrWhiteSpace(LoadingText);

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
            StartupPerformanceTracker.Mark("主窗口初始化开始");
            var appSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            _loginSessionStateMachine.UpdateSettings(appSettings);
            await _uiDispatcher.RunAsync(() => ApplyClientAppInfoState(appSettings.IsSetClientAppInfo)).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("应用设置加载完成");
            EnsureDefaultContentLoaded();
            StartupPerformanceTracker.Mark("默认内容装载完成");
            await _appStartupCoordinator.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("启动协调器初始化完成");

            if (!IsClientAppInfoConfigured)
            {
                await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
                StartupPerformanceTracker.Mark("首屏前PLC启动连接检查完成（未配置客户端信息）");
                return;
            }

            using (_uiBusyService.Enter(LocalizedText.Get("Services.PlcStartupConnection.Connecting")))
            {
                await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            }

            StartupPerformanceTracker.Mark("首屏后PLC启动连接完成");
        }

        private bool CanLogout() => IsLoggedIn;

        private void OnTabChanged(int index)
        {
            _selectedTabIndex = index;
            SelectedContent = ResolveTabContent(index);
        }

        private void OnOpenLoginRequested()
        {
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAppSettingsSaved(object? sender, AppSettings settings)
        {
            _uiDispatcher.Run(() =>
            {
                _loginSessionStateMachine.UpdateSettings(settings);
                ApplyClientAppInfoState(settings.IsSetClientAppInfo);
            });
        }

        private async Task LogoutAsync()
        {
            await _loginService.LogoutAsync().ConfigureAwait(false);
        }

        private void OnLoginSessionStateChanged(object? sender, EventArgs e)
        {
            _uiDispatcher.Run(() => ApplyLoginState(_loginSessionStateMachine.Current));
        }

        private void OnUiBusyServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IUiBusyService.IsBusy)
                || e.PropertyName == nameof(IUiBusyService.BusyMessage))
            {
                _uiDispatcher.Run(() =>
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(IsNotBusy));
                    OnPropertyChanged(nameof(LoadingText));
                    OnPropertyChanged(nameof(HasLoadingText));
                    LogoutCommand.NotifyCanExecuteChanged();
                });
            }
        }

        private void ApplyLoginState(LoginSessionState state)
        {
            var wasLoggedIn = IsLoggedIn;
            var currentUser = state.CurrentUser;
            IsLoggedIn = state.IsLoggedIn;
            CurrentUserWorkIdText = currentUser is null
                ? LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserWorkIdEmpty")
                : LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserWorkId", currentUser.WorkId);

            if (currentUser is null)
            {
                CurrentUserAccessLevelText = LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserAccessLevelEmpty");
                RefreshSelectedContentForCurrentStateIfNeeded(wasLoggedIn, IsLoggedIn);
                return;
            }

            var remaining = TimeSpan.FromSeconds(Math.Max(state.RemainingAutoLogoutSeconds, 0));
            CurrentUserAccessLevelText = LocalizedText.Format(
                "ViewModels.MainWindowVm.CurrentUserAccessLevelCountdown",
                currentUser.AccessLevel,
                remaining.ToString("mm\\:ss"));
            RefreshSelectedContentForCurrentStateIfNeeded(wasLoggedIn, IsLoggedIn);
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

            SelectedContent = ResolveTabContent(_selectedTabIndex);
            _defaultContentPending = false;
        }

        private void RefreshSelectedContentForCurrentStateIfNeeded(bool wasLoggedIn, bool isLoggedIn)
        {
            if (wasLoggedIn == isLoggedIn)
            {
                return;
            }

            if (!IsClientAppInfoConfigured || Volatile.Read(ref _initializeStarted) == 0)
            {
                return;
            }

            SelectedContent = ResolveTabContent(_selectedTabIndex);
        }

        private object ResolveTabContent(int index)
        {
            if (!_isClientAppInfoConfigured)
            {
                return _serviceProvider.GetRequiredService<ClientAppInfoUserControl>();
            }

            if (!IsLoggedIn && index != 1)
            {
                return _serviceProvider.GetRequiredService<NeedLoginUserControl>();
            }

            return index switch
            {
                1 => _serviceProvider.GetRequiredService<ClientAppInfoUserControl>(),
                2 => _serviceProvider.GetRequiredService<PartManagementUserControl>(),
                3 => _serviceProvider.GetRequiredService<PartUpdateRecordUserControl>(),
                4 => _serviceProvider.GetRequiredService<UserConfigUserControl>(),
                _ => _serviceProvider.GetRequiredService<ReplacePartUserControl>()
            };
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
    }
}