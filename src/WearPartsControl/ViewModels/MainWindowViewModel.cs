using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.UserControls;

namespace WearPartsControl.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        private string? _selectedTabHeader;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly ILoginService _loginService;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IReadOnlyList<string> _allTabs;
        private string _currentUserWorkIdText = "工号：--";
        private string _currentUserAccessLevelText = "权限：--";
        private IEnumerable<string> _tabs = Array.Empty<string>();
        private bool _isLoggedIn;
        private bool _isClientAppInfoConfigured;

        public MainWindowViewModel(
            ILocalizationService localizationService,
            IServiceProvider serviceProvider,
            ICurrentUserAccessor currentUserAccessor,
            ILoginService loginService,
            IAppSettingsService appSettingsService)
        {
            Title = localizationService["MainWindow.Title"];
            TabChangedCommand = new RelayCommand<int>(OnTabChanged);
            OpenLoginCommand = new RelayCommand(OnOpenLoginRequested);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);
            _serviceProvider = serviceProvider;
            _currentUserAccessor = currentUserAccessor;
            _loginService = loginService;
            _selectedContent = _serviceProvider.GetRequiredService<ReplacePartUserControl>();
            _appSettingsService = appSettingsService;
            _allTabs = localizationService.Catalog.MainWindow.Tabs.ToArray();

            SoftwareVersionText = $"软件版本：{ResolveVersion()}";
            _currentUserAccessor.CurrentUserChanged += OnCurrentUserChanged;
            _appSettingsService.SettingsSaved += OnAppSettingsSaved;
            UpdateCurrentUserState();

            var appSettings = _appSettingsService.GetAsync(default).GetAwaiter().GetResult();
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

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            private set
            {
                if (SetProperty(ref _isLoggedIn, value))
                {
                    OnPropertyChanged(nameof(LoginButtonText));
                    LogoutCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsClientAppInfoConfigured
        {
            get => _isClientAppInfoConfigured;
            private set => SetProperty(ref _isClientAppInfoConfigured, value);
        }

        public string LoginButtonText => IsLoggedIn ? "切换登录" : "登录";

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
                dispatcher.Invoke(() => ApplyClientAppInfoState(settings.IsSetClientAppInfo));
                return;
            }

            ApplyClientAppInfoState(settings.IsSetClientAppInfo);
        }

        private async Task LogoutAsync()
        {
            await _loginService.LogoutAsync().ConfigureAwait(false);
        }

        private void OnCurrentUserChanged(object? sender, EventArgs e)
        {
            UpdateCurrentUserState();
        }

        private void UpdateCurrentUserState()
        {
            var currentUser = _currentUserAccessor.CurrentUser;
            IsLoggedIn = currentUser is not null;
            CurrentUserWorkIdText = currentUser is null ? "工号：--" : $"工号：{currentUser.WorkId}";
            CurrentUserAccessLevelText = currentUser is null ? "权限：--" : $"权限：{currentUser.AccessLevel}";
        }

        private void ApplyClientAppInfoState(bool isConfigured)
        {
            var stateChanged = _isClientAppInfoConfigured != isConfigured;
            IsClientAppInfoConfigured = isConfigured;

            if (isConfigured)
            {
                Tabs = _allTabs.ToArray();
                if (stateChanged || SelectedContent is ClientAppInfoUserControl)
                {
                    SelectedContent = _serviceProvider.GetRequiredService<ReplacePartUserControl>();
                }

                return;
            }

            Tabs = _allTabs.Count > 1
                ? new[] { _allTabs[1] }
                : _allTabs.ToArray();
            SelectedContent = _serviceProvider.GetRequiredService<ClientAppInfoUserControl>();
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