using System.Windows.Threading;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class LoginWindowViewModelTests
{
    [Fact]
    public async Task LoginCommand_ShouldUseConfiguredResourceAndSiteCode()
    {
        var loginService = new StubLoginService();
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService(),
            new StubUiDispatcher());

        bool? dialogResult = null;
        viewModel.RequestClose += (_, result) => dialogResult = result;

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("RES-001", loginService.ResourceNumber);
        Assert.Equal("SITE-01", loginService.SiteCode);
        Assert.True(loginService.IsIdCard);
        Assert.True(dialogResult);
    }

    [Fact]
    public async Task InitializeAsync_WhenUseWorkNumberLoginEnabled_ShouldSwitchToWorkNumberMode()
    {
        var loginService = new StubLoginService();
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService { UseWorkNumberLogin = true },
            new StubUiDispatcher());

        await viewModel.InitializeAsync();
        viewModel.AuthId = "WORK-01";

        viewModel.RejectManualInput();
        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.True(viewModel.UseWorkNumberLogin);
        Assert.False(viewModel.RequiresCardScan);
        Assert.Equal(LocalizedText.Get("ViewModels.LoginWindowVm.PromptEnterWorkNumber"), viewModel.LoginPrompt);
        Assert.Equal("WORK-01", viewModel.AuthId);
        Assert.False(loginService.IsIdCard);
    }

    [Fact]
    public async Task InitializeAsync_WhenClientSiteCodeMissing_ShouldShowPromptAndKeepSiteEmpty()
    {
        var loginService = new StubLoginService();
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(siteCode: "  "),
            new StubAppSettingsService(),
            new StubUiDispatcher());

        await viewModel.InitializeAsync();

        Assert.Equal(string.Empty, viewModel.SiteCode);
        Assert.Equal(LocalizedText.Format("ViewModels.LoginWindowVm.ClientConfigurationSiteMissing", "RES-001"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoginCommand_WhenExecuting_ShouldSetBusyForLoginWindowLoading()
    {
        var loginService = new StubLoginService
        {
            LoginTaskSource = new TaskCompletionSource<MhrUser?>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService(),
            new StubUiDispatcher());

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        var executeTask = viewModel.LoginCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBusy);

        Assert.True(viewModel.IsBusy);
        Assert.Equal(LocalizedText.Get("ViewModels.LoginWindowVm.LoggingIn"), viewModel.StatusMessage);

        loginService.LoginTaskSource.SetResult(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 1
        });

        await executeTask;
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_WhenLoginSucceedsImmediately_ShouldKeepLoadingVisibleBeforeClosing()
    {
        var delaySignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loginService = new StubLoginService();
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService(),
            new StubUiDispatcher(),
            TimeSpan.FromMilliseconds(500),
            (delay, cancellationToken) =>
            {
                Assert.True(delay > TimeSpan.Zero);
                cancellationToken.Register(() => delaySignal.TrySetCanceled(cancellationToken));
                return delaySignal.Task;
            });

        bool? dialogResult = null;
        viewModel.RequestClose += (_, result) => dialogResult = result;

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        var executeTask = viewModel.LoginCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBusy);

        Assert.True(viewModel.IsBusy);
        Assert.Null(dialogResult);

        delaySignal.SetResult(true);
        await executeTask;

        Assert.True(dialogResult);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_WhenUserNotFound_ShouldClearInputAndRaiseClearEvent()
    {
        var loginService = new StubLoginService { LoginResult = null };
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService(),
            new StubUiDispatcher());
        var clearRaised = false;
        viewModel.RequestClearInput += (_, _) => clearRaised = true;

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.True(clearRaised);
        Assert.Equal(string.Empty, viewModel.AuthId);
        Assert.Equal(LocalizedText.Get("ViewModels.LoginWindowVm.UserNotFound"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoginCommand_WhenLoginThrows_ShouldClearInputAndKeepErrorMessage()
    {
        var loginService = new StubLoginService { LoginException = new InvalidOperationException("登录失败") };
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService(),
            new StubUiDispatcher());
        var clearRaised = false;
        viewModel.RequestClearInput += (_, _) => clearRaised = true;

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.True(clearRaised);
        Assert.Equal(string.Empty, viewModel.AuthId);
        Assert.Equal("登录失败", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LocalizationRefresh_ShouldUpdatePromptAndLocalizedStatus()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var viewModel = new LoginWindowViewModel(
            new StubLoginService(),
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService { UseWorkNumberLogin = true },
            new StubUiDispatcher());

        await viewModel.InitializeAsync();

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal(LocalizedText.Get("ViewModels.LoginWindowVm.PromptEnterWorkNumber"), viewModel.LoginPrompt);
        Assert.Equal(LocalizedText.Get("ViewModels.LoginWindowVm.PromptEnterWorkNumber"), viewModel.StatusMessage);
    }

    private sealed class StubLoginService : ILoginService
    {
        public MhrUser? LoginResult { get; set; } = new MhrUser
        {
            WorkId = "WORK-01",
            AccessLevel = 1
        };

        public Exception? LoginException { get; set; }

        public string ResourceNumber { get; private set; } = string.Empty;

        public string SiteCode { get; private set; } = string.Empty;

        public bool IsIdCard { get; private set; }

        public TaskCompletionSource<MhrUser?>? LoginTaskSource { get; set; }

        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            SiteCode = factory;
            ResourceNumber = resourceId;
            IsIdCard = isIdCard;

            if (LoginException is not null)
            {
                return Task.FromException<MhrUser?>(LoginException);
            }

            if (LoginTaskSource is not null)
            {
                return LoginTaskSource.Task;
            }

            if (LoginResult is not null)
            {
                LoginResult.CardId = authId;
            }

            return Task.FromResult(LoginResult);
        }

        public MhrUser? GetCurrentUser() => null;

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public bool UseWorkNumberLogin { get; set; }

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = "RES-001",
                LoginInputMaxIntervalMilliseconds = 88,
                UseWorkNumberLogin = UseWorkNumberLogin
            });
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubClientAppConfigurationRepository : IClientAppConfigurationRepository
    {
        private readonly string _siteCode;

        public StubClientAppConfigurationRepository(string siteCode = "SITE-01")
        {
            _siteCode = siteCode;
        }

        public IUnitOfWork UnitOfWork => throw new NotSupportedException();

        public Task<ClientAppConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ClientAppConfigurationEntity?>(new ClientAppConfigurationEntity
            {
                SiteCode = _siteCode,
                FactoryCode = "FACTORY-01",
                AreaCode = "AREA-01",
                ProcedureCode = "PROC-01",
                EquipmentCode = "EQ-01",
                ResourceNumber = resourceNumber,
                PlcProtocolType = "SiemensS7",
                PlcIpAddress = "127.0.0.1",
                PlcPort = 102
            });
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return GetByResourceNumberAsync(resourceNumber, cancellationToken);
        }

        public Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ClientAppConfigurationEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var index = 0; index < 20; index++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }
}