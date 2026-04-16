using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.ViewModels
{
    public class LoginWindowViewModel : ObservableObject
    {
        private readonly ILoginService _loginService;
        private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
        private readonly IAppSettingsService _appSettingsService;
        private string _authId = string.Empty;
        private string _statusMessage = "请刷卡登录";
        private string _resourceNumber = string.Empty;
        private string _siteCode = string.Empty;
        private int _loginInputMaxIntervalMilliseconds = 80;
        private bool _isBusy;

        public LoginWindowViewModel(
            ILoginService loginService,
            IClientAppConfigurationRepository clientAppConfigurationRepository,
            IAppSettingsService appSettingsService)
        {
            _loginService = loginService;
            _clientAppConfigurationRepository = clientAppConfigurationRepository;
            _appSettingsService = appSettingsService;
            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        }

        public event EventHandler<bool?>? RequestClose;

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
            private set => SetProperty(ref _resourceNumber, value);
        }

        public string SiteCode
        {
            get => _siteCode;
            private set => SetProperty(ref _siteCode, value);
        }

        public int LoginInputMaxIntervalMilliseconds
        {
            get => _loginInputMaxIntervalMilliseconds;
            private set => SetProperty(ref _loginInputMaxIntervalMilliseconds, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    LoginCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand LoginCommand { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            ResourceNumber = settings.ResourceNumber;
            LoginInputMaxIntervalMilliseconds = settings.LoginInputMaxIntervalMilliseconds <= 0
                ? 80
                : settings.LoginInputMaxIntervalMilliseconds;

            if (string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SiteCode = string.Empty;
                StatusMessage = "请先在配置中设置资源号。";
                return;
            }

            var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(ResourceNumber, cancellationToken).ConfigureAwait(false);
            if (clientAppConfiguration is null)
            {
                SiteCode = string.Empty;
                StatusMessage = $"未找到资源号 {ResourceNumber} 对应的客户端配置。";
                return;
            }

            SiteCode = clientAppConfiguration.SiteCode;
            StatusMessage = "请刷卡登录";
        }

        public void RejectManualInput()
        {
            AuthId = string.Empty;
            StatusMessage = $"检测到输入间隔超过 {LoginInputMaxIntervalMilliseconds} ms，请使用刷卡器登录。";
        }

        public void ClearInput()
        {
            AuthId = string.Empty;
        }

        private bool CanLogin()
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(AuthId);
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(ResourceNumber) || string.IsNullOrWhiteSpace(SiteCode))
            {
                await InitializeAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(ResourceNumber) || string.IsNullOrWhiteSpace(SiteCode))
                {
                    return;
                }
            }

            var authId = AuthId.Trim();
            if (string.IsNullOrWhiteSpace(authId))
            {
                StatusMessage = "请先刷卡后再登录。";
                return;
            }

            IsBusy = true;
            StatusMessage = "正在登录...";

            try
            {
                var user = await _loginService.LoginAsync(authId, SiteCode, ResourceNumber, isIdCard: true).ConfigureAwait(false);
                if (user is null)
                {
                    StatusMessage = "未找到对应用户，请确认卡号或权限配置。";
                    return;
                }

                StatusMessage = $"登录成功，工号 {user.WorkId}";
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
