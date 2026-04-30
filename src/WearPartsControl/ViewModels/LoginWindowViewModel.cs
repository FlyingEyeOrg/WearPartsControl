using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.ViewModels
{
    public class LoginWindowViewModel : LocalizedViewModelBase
    {
        private const int DefaultLoginInputMaxIntervalMilliseconds = 80;
        private const int DebugLoginInputMaxIntervalMilliseconds = 2000;

        private readonly ILoginService _loginService;
        private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private string _authId = string.Empty;
        private string _statusMessage = LocalizedText.Get("ViewModels.LoginWindowVm.PromptSwipeCard");
        private string _resourceNumber = string.Empty;
        private string _siteCode = string.Empty;
        private int _loginInputMaxIntervalMilliseconds = DefaultLoginInputMaxIntervalMilliseconds;
        private bool _useWorkNumberLogin;
        private bool _isBusy;
        private Func<string>? _statusMessageFactory;

        public LoginWindowViewModel(
            ILoginService loginService,
            IClientAppConfigurationRepository clientAppConfigurationRepository,
            IAppSettingsService appSettingsService,
            IUiDispatcher uiDispatcher,
            TimeSpan? minimumBusyDuration = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        {
            _loginService = loginService;
            _clientAppConfigurationRepository = clientAppConfigurationRepository;
            _appSettingsService = appSettingsService;
            _uiDispatcher = uiDispatcher;
            _delayAsync = delayAsync ?? Task.Delay;
            MinimumBusyDuration = minimumBusyDuration ?? TimeSpan.FromMilliseconds(500);
            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
            SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.LoginWindowVm.PromptSwipeCard"));
        }

        public event EventHandler<bool?>? RequestClose;

        public event EventHandler? RequestClearInput;

        public string AuthId
        {
            get => _authId;
            set
            {
                if (SetProperty(ref _authId, value))
                {
                    LoginCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string ResourceNumber
        {
            get => _resourceNumber;
            private set
            {
                if (SetProperty(ref _resourceNumber, value))
                {
                    NotifyLoginCommandCanExecuteChanged();
                }
            }
        }

        public string SiteCode
        {
            get => _siteCode;
            private set
            {
                if (SetProperty(ref _siteCode, value))
                {
                    NotifyLoginCommandCanExecuteChanged();
                }
            }
        }

        public int LoginInputMaxIntervalMilliseconds
        {
            get => _loginInputMaxIntervalMilliseconds;
            private set => SetProperty(ref _loginInputMaxIntervalMilliseconds, value);
        }

        public bool UseWorkNumberLogin
        {
            get => _useWorkNumberLogin;
            private set
            {
                if (SetProperty(ref _useWorkNumberLogin, value))
                {
                    OnPropertyChanged(nameof(RequiresCardScan));
                    OnPropertyChanged(nameof(LoginPrompt));
                }
            }
        }

        public bool RequiresCardScan => !UseWorkNumberLogin;

        public string LoginPrompt => UseWorkNumberLogin
            ? LocalizedText.Get("ViewModels.LoginWindowVm.PromptEnterWorkNumber")
            : LocalizedText.Get("ViewModels.LoginWindowVm.PromptSwipeCard");

        public TimeSpan MinimumBusyDuration { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    NotifyLoginCommandCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand LoginCommand { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _appSettingsService.GetAsync(cancellationToken);
            ResourceNumber = settings.ResourceNumber;
            LoginInputMaxIntervalMilliseconds = ResolveLoginInputMaxIntervalMilliseconds(settings.LoginInputMaxIntervalMilliseconds);
            UseWorkNumberLogin = settings.UseWorkNumberLogin;

            if (string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SiteCode = string.Empty;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.LoginWindowVm.ResourceNumberMissing"));
                return;
            }

            var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(ResourceNumber, cancellationToken);
            if (clientAppConfiguration is null)
            {
                SiteCode = string.Empty;
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.LoginWindowVm.ClientConfigurationNotFound", ResourceNumber));
                return;
            }

            if (string.IsNullOrWhiteSpace(clientAppConfiguration.SiteCode))
            {
                SiteCode = string.Empty;
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.LoginWindowVm.ClientConfigurationSiteMissing", ResourceNumber));
                return;
            }

            SiteCode = clientAppConfiguration.SiteCode.Trim();
            SetLocalizedStatusMessage(() => LoginPrompt);
        }

        public void RejectManualInput()
        {
            if (!RequiresCardScan)
            {
                return;
            }

            AuthId = string.Empty;
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.LoginWindowVm.ManualInputRejected", LoginInputMaxIntervalMilliseconds));
        }

        public void ClearInput()
        {
            AuthId = string.Empty;
        }

        private static int ResolveLoginInputMaxIntervalMilliseconds(int configuredMilliseconds)
        {
#if DEBUG
            return DebugLoginInputMaxIntervalMilliseconds;
#else
            return configuredMilliseconds <= 0
                ? DefaultLoginInputMaxIntervalMilliseconds
                : configuredMilliseconds;
#endif
        }

        private bool CanLogin()
        {
            return !IsBusy
                && !string.IsNullOrWhiteSpace(AuthId)
                && !string.IsNullOrWhiteSpace(ResourceNumber)
                && !string.IsNullOrWhiteSpace(SiteCode);
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(ResourceNumber) || string.IsNullOrWhiteSpace(SiteCode))
            {
                await InitializeAsync();
                if (string.IsNullOrWhiteSpace(ResourceNumber) || string.IsNullOrWhiteSpace(SiteCode))
                {
                    return;
                }
            }

            var authId = AuthId.Trim();
            if (string.IsNullOrWhiteSpace(authId))
            {
                await _uiDispatcher.RunAsync(() => SetLocalizedStatusMessage(() => LocalizedText.Get(UseWorkNumberLogin
                    ? "ViewModels.LoginWindowVm.AuthIdMissingWorkNumber"
                    : "ViewModels.LoginWindowVm.AuthIdMissing")));
                return;
            }

            var busyEnteredAt = DateTimeOffset.UtcNow;

            await _uiDispatcher.RunAsync(() =>
            {
                IsBusy = true;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.LoginWindowVm.LoggingIn"));
            });

            await _uiDispatcher.RenderAsync();

            try
            {
                var user = await _loginService.LoginAsync(authId, SiteCode, ResourceNumber, isIdCard: !UseWorkNumberLogin);
                await EnsureMinimumBusyDurationAsync(busyEnteredAt);

                if (user is null)
                {
                    await _uiDispatcher.RunAsync(() =>
                    {
                        AuthId = string.Empty;
                        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.LoginWindowVm.UserNotFound"));
                        RequestClearInput?.Invoke(this, EventArgs.Empty);
                    });
                    return;
                }

                await _uiDispatcher.RunAsync(() =>
                {
                    SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.LoginWindowVm.LoginSucceeded", user.WorkId));
                    RequestClose?.Invoke(this, true);
                });
            }
            catch (Exception ex)
            {
                await EnsureMinimumBusyDurationAsync(busyEnteredAt);
                await _uiDispatcher.RunAsync(() =>
                {
                    AuthId = string.Empty;
                    SetRawStatusMessage(ex.Message);
                    RequestClearInput?.Invoke(this, EventArgs.Empty);
                });
            }
            finally
            {
                await _uiDispatcher.RunAsync(() => IsBusy = false);
            }
        }

        private async Task EnsureMinimumBusyDurationAsync(DateTimeOffset busyEnteredAt, CancellationToken cancellationToken = default)
        {
            var elapsed = DateTimeOffset.UtcNow - busyEnteredAt;
            var remaining = MinimumBusyDuration - elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            await _delayAsync(remaining, cancellationToken);
        }

        private void NotifyLoginCommandCanExecuteChanged()
        {
            _ = _uiDispatcher.RunAsync(LoginCommand.NotifyCanExecuteChanged);
        }

        protected override void OnLocalizationRefreshed()
        {
            OnPropertyChanged(nameof(LoginPrompt));

            if (_statusMessageFactory is not null)
            {
                StatusMessage = _statusMessageFactory();
            }
        }

        private void SetLocalizedStatusMessage(Func<string> factory)
        {
            _statusMessageFactory = factory;
            StatusMessage = factory();
        }

        private void SetRawStatusMessage(string message)
        {
            _statusMessageFactory = null;
            StatusMessage = message;
        }
    }
}
