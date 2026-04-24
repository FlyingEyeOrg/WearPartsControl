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
using WearPartsControl.ApplicationServices.ClientAppInfo;
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
        private readonly ILocalizationService _localizationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoginService _loginService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IClientAppInfoService _clientAppInfoService;
        private readonly IUiBusyService _uiBusyService;
        private readonly IPlcStartupConnectionService _plcStartupConnectionService;
        private readonly ILoginSessionStateMachine _loginSessionStateMachine;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IAppStartupCoordinator _appStartupCoordinator;
        private string[] _allTabs;
        private string _brandTitle = string.Empty;
        private string _title = string.Empty;
        private string _softwareVersionText = string.Empty;
        private bool _isStartupBusy;
        private string _startupLoadingText = string.Empty;
        private string _currentUserWorkIdText = string.Empty;
        private string _currentUserAccessLevelText = string.Empty;
        private IEnumerable<string> _tabs = Array.Empty<string>();
        private bool _isLoggedIn;
        private bool _isClientAppInfoConfigured;
        private int _selectedTabIndex;
        private int _initializeStarted;
        private bool _defaultContentPending;
        private string _procedureCode = string.Empty;

        public MainWindowViewModel(
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            ILoginService loginService,
            IAppSettingsService appSettingsService,
            IClientAppInfoService clientAppInfoService,
            IUiBusyService uiBusyService,
            IPlcStartupConnectionService plcStartupConnectionService,
            ILoginSessionStateMachine loginSessionStateMachine,
            IUiDispatcher uiDispatcher,
            IAppStartupCoordinator appStartupCoordinator)
        {
            _localizationService = localizationService;
            TabChangedCommand = new RelayCommand<int>(OnTabChanged);
            OpenLoginCommand = new RelayCommand(OnOpenLoginRequested);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync, CanLogout);
            _serviceProvider = serviceProvider;
            _loginService = loginService;
            _uiBusyService = uiBusyService;
            _clientAppInfoService = clientAppInfoService;
            _plcStartupConnectionService = plcStartupConnectionService;
            _loginSessionStateMachine = loginSessionStateMachine;
            _uiDispatcher = uiDispatcher;
            _appStartupCoordinator = appStartupCoordinator;
            _selectedContent = PlaceholderContent;
            _appSettingsService = appSettingsService;
            _allTabs = Array.Empty<string>();

            RefreshLocalizedShellState(refreshSelectedContent: false);
            _loginSessionStateMachine.StateChanged += OnLoginSessionStateChanged;
            _appSettingsService.SettingsSaved += OnAppSettingsSaved;
            _uiBusyService.PropertyChanged += OnUiBusyServicePropertyChanged;
            WeakEventManager<LocalizationBindingSource, EventArgs>.AddHandler(
                LocalizationBindingSource.Instance,
                nameof(LocalizationBindingSource.Refreshed),
                OnLocalizationRefreshed);
            ApplyLoginState(_loginSessionStateMachine.Current);
            ApplyClientAppInfoState(false);
        }

        public event EventHandler? LoginRequested;

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public string BrandTitle
        {
            get => _brandTitle;
            private set => SetProperty(ref _brandTitle, value);
        }

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

        public string SoftwareVersionText
        {
            get => _softwareVersionText;
            private set => SetProperty(ref _softwareVersionText, value);
        }

        public bool IsBusy => _isStartupBusy || _uiBusyService.IsBusy;

        public string LoadingText => _isStartupBusy ? _startupLoadingText : _uiBusyService.BusyMessage;

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
            var appSettings = default(AppSettings);
            await ExecuteStartupActionAsync(
                LocalizedText.Get("ViewModels.MainWindowVm.LoadingAppSettings"),
                async () =>
                {
                    appSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
                    _loginSessionStateMachine.UpdateSettings(appSettings);
                    _procedureCode = appSettings.IsSetClientAppInfo
                        ? (await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false)).ProcedureCode?.Trim() ?? string.Empty
                        : string.Empty;
                    await _uiDispatcher.RunAsync(() => ApplyClientAppInfoState(appSettings.IsSetClientAppInfo)).ConfigureAwait(false);
                }).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("应用设置加载完成");
            await ExecuteStartupActionAsync(
                LocalizedText.Get("ViewModels.MainWindowVm.LoadingDefaultContent"),
                () =>
                {
                    EnsureDefaultContentLoaded();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("默认内容装载完成");
            await ExecuteStartupActionAsync(
                LocalizedText.Get("ViewModels.MainWindowVm.InitializingDatabase"),
                reportLoadingAsync => _appStartupCoordinator.EnsureInitializedAsync(reportLoadingAsync, cancellationToken)).ConfigureAwait(false);
            StartupPerformanceTracker.Mark("启动协调器初始化完成");
        }

        private async Task ExecuteStartupActionAsync(string loadingText, Func<Task> action)
        {
            await BeginStartupBusyAsync(loadingText).ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                await EndStartupBusyAsync().ConfigureAwait(false);
            }
        }

        private async Task ExecuteStartupActionAsync(string initialLoadingText, Func<Func<string, Task>, Task> action)
        {
            await BeginStartupBusyAsync(initialLoadingText).ConfigureAwait(false);
            try
            {
                await action(UpdateStartupLoadingTextAsync).ConfigureAwait(false);
            }
            finally
            {
                await EndStartupBusyAsync().ConfigureAwait(false);
            }
        }

        private async Task BeginStartupBusyAsync(string loadingText)
        {
            await _uiDispatcher.RunAsync(() => SetStartupBusyState(true, loadingText)).ConfigureAwait(false);
            await _uiDispatcher.RenderAsync().ConfigureAwait(false);
        }

        private async Task UpdateStartupLoadingTextAsync(string loadingText)
        {
            await _uiDispatcher.RunAsync(() => SetStartupBusyState(true, loadingText)).ConfigureAwait(false);
            await _uiDispatcher.RenderAsync().ConfigureAwait(false);
        }

        private Task EndStartupBusyAsync()
        {
            return _uiDispatcher.RunAsync(() => SetStartupBusyState(false, string.Empty));
        }

        private void SetStartupBusyState(bool isBusy, string loadingText)
        {
            var hasChanged = false;

            if (_isStartupBusy != isBusy)
            {
                _isStartupBusy = isBusy;
                hasChanged = true;
            }

            if (!string.Equals(_startupLoadingText, loadingText, StringComparison.Ordinal))
            {
                _startupLoadingText = loadingText;
                hasChanged = true;
            }

            if (!hasChanged)
            {
                return;
            }

            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(LoadingText));
            OnPropertyChanged(nameof(HasLoadingText));
            LogoutCommand.NotifyCanExecuteChanged();
        }

        private bool CanLogout() => IsLoggedIn;

        private void OnTabChanged(int index)
        {
            _selectedTabIndex = index;
            SelectedTabHeader = GetVisibleTabs().ElementAtOrDefault(index);
            UpdateSelectedContent(index);
        }

        private void OnOpenLoginRequested()
        {
            LoginRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAppSettingsSaved(object? sender, AppSettings settings)
        {
            _ = RefreshClientAppInfoStateAsync(settings);
        }

        private async Task RefreshClientAppInfoStateAsync(AppSettings settings)
        {
            var wasConfigured = IsClientAppInfoConfigured;
            var previousProcedureCode = _procedureCode;
            var procedureCode = settings.IsSetClientAppInfo
                ? (await _clientAppInfoService.GetAsync().ConfigureAwait(false)).ProcedureCode?.Trim() ?? string.Empty
                : string.Empty;

            await _uiDispatcher.RunAsync(() =>
            {
                _loginSessionStateMachine.UpdateSettings(settings);
                _procedureCode = procedureCode;
                ApplyClientAppInfoState(
                    settings.IsSetClientAppInfo,
                    refreshSelectedContent: wasConfigured != settings.IsSetClientAppInfo
                        || !string.Equals(previousProcedureCode, procedureCode, StringComparison.Ordinal));
            }).ConfigureAwait(false);
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

        private void OnLocalizationRefreshed(object? sender, EventArgs e)
        {
            _uiDispatcher.Run(() => RefreshLocalizedShellState(refreshSelectedContent: true));
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

        private void RefreshLocalizedShellState(bool refreshSelectedContent)
        {
            Title = _localizationService["MainWindow.Title"];
            BrandTitle = _localizationService["MainWindowView.BrandTitle"];
            SoftwareVersionText = LocalizedText.Format("ViewModels.MainWindowVm.SoftwareVersion", ResolveVersion());
            _allTabs = _localizationService.Catalog.MainWindow.Tabs.ToArray();

            var visibleTabs = GetVisibleTabs().ToArray();
            Tabs = visibleTabs;
            EnsureSelectedTabIndexIsValid(visibleTabs);

            if (_isClientAppInfoConfigured && refreshSelectedContent && Volatile.Read(ref _initializeStarted) == 1)
            {
                UpdateSelectedContent(_selectedTabIndex);
            }

            ApplyLoginState(_loginSessionStateMachine.Current);
        }

        private void ApplyClientAppInfoState(bool isConfigured, bool refreshSelectedContent = true)
        {
            IsClientAppInfoConfigured = isConfigured;
            var visibleTabs = GetVisibleTabs().ToArray();

            if (isConfigured)
            {
                Tabs = visibleTabs;
                EnsureSelectedTabIndexIsValid(visibleTabs);

                if (ReferenceEquals(SelectedContent, PlaceholderContent))
                {
                    _defaultContentPending = true;
                    EnsureDefaultContentLoaded();
                }
                else
                {
                    _defaultContentPending = false;
                    if (refreshSelectedContent && Volatile.Read(ref _initializeStarted) == 1)
                    {
                        UpdateSelectedContent(_selectedTabIndex);
                    }
                }

                return;
            }

            _defaultContentPending = false;
            Tabs = visibleTabs;
            _selectedTabIndex = 0;
            SelectedTabHeader = visibleTabs.ElementAtOrDefault(0);
            UpdateSelectedContent(typeof(ClientAppInfoUserControl));
        }

        private void EnsureDefaultContentLoaded()
        {
            if (!_defaultContentPending)
            {
                return;
            }

            EnsureSelectedTabIndexIsValid(GetVisibleTabs().ToArray());
            UpdateSelectedContent(_selectedTabIndex);
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

            if (_selectedTabIndex == 1)
            {
                return;
            }

            UpdateSelectedContent(_selectedTabIndex);
        }

        private void UpdateSelectedContent(int index)
        {
            UpdateSelectedContent(ResolveTabContentType(index));
        }

        private void UpdateSelectedContent(Type contentType)
        {
            if (contentType.IsInstanceOfType(SelectedContent))
            {
                return;
            }

            SelectedContent = _serviceProvider.GetRequiredService(contentType);
        }

        private Type ResolveTabContentType(int index)
        {
            if (!_isClientAppInfoConfigured)
            {
                return typeof(ClientAppInfoUserControl);
            }

            var visibleTabs = GetVisibleTabs().ToArray();
            if (visibleTabs.Length == 0)
            {
                return typeof(ClientAppInfoUserControl);
            }

            if (index < 0 || index >= visibleTabs.Length)
            {
                index = 0;
            }

            var tabHeader = visibleTabs[index];
            SelectedTabHeader = tabHeader;

            if (!IsLoggedIn && index != 1)
            {
                return typeof(NeedLoginUserControl);
            }

            if (string.Equals(tabHeader, _allTabs[1], StringComparison.Ordinal))
            {
                return typeof(ClientAppInfoUserControl);
            }

            if (string.Equals(tabHeader, _allTabs[2], StringComparison.Ordinal))
            {
                return typeof(PartManagementUserControl);
            }

            if (string.Equals(tabHeader, _allTabs[3], StringComparison.Ordinal))
            {
                return typeof(ToolChangeManagementUserControl);
            }

            if (string.Equals(tabHeader, _allTabs[4], StringComparison.Ordinal))
            {
                return typeof(PartUpdateRecordUserControl);
            }

            if (string.Equals(tabHeader, _allTabs[5], StringComparison.Ordinal))
            {
                return typeof(UserConfigUserControl);
            }

            return typeof(ReplacePartUserControl);
        }

        private IEnumerable<string> GetVisibleTabs()
        {
            if (!_isClientAppInfoConfigured)
            {
                return _allTabs.Length > 1
                    ? new[] { _allTabs[1] }
                    : _allTabs.ToArray();
            }

            return string.Equals(_procedureCode, "模切分条", StringComparison.Ordinal)
                ? _allTabs.ToArray()
                : _allTabs.Where((_, index) => index != 3).ToArray();
        }

        private void EnsureSelectedTabIndexIsValid(IReadOnlyList<string> visibleTabs)
        {
            if (visibleTabs.Count == 0)
            {
                _selectedTabIndex = 0;
                SelectedTabHeader = null;
                return;
            }

            if (_selectedTabIndex < 0 || _selectedTabIndex >= visibleTabs.Count)
            {
                _selectedTabIndex = 0;
            }

            SelectedTabHeader = visibleTabs[_selectedTabIndex];
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