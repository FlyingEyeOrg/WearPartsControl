using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class UserConfigViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadSavedUserConfig()
    {
        var service = new StubUserConfigService
        {
            Current = new UserConfig
            {
                MeResponsibleWorkId = "ME001",
                PrdResponsibleWorkId = "PRD001",
                ComAccessToken = "token",
                ComSecret = "secret",
                ComNotificationEnabled = true,
                ComPushUrl = "https://example.com/push",
                ComDeIpaasKeyAuth = "auth-key",
                ComAgentId = 1642112457,
                ComGroupTemplateId = 303686603505665,
                ComWorkTemplateId = 303717003821057,
                ComUserType = "ding",
                ComTimeoutMilliseconds = 10000,
                SpacerValidationEnabled = false,
                SpacerValidationUrl = "https://spacer/api",
                SpacerValidationTimeoutMilliseconds = 8000,
                SpacerValidationIgnoreServerCertificateErrors = false,
                SpacerValidationCodeSeparator = "-",
                SpacerValidationExpectedSegmentCount = 9
            }
        };
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), service, new StubComNotificationService(), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        Assert.Equal("ME001", viewModel.MeResponsibleWorkId);
        Assert.Equal("PRD001", viewModel.PrdResponsibleWorkId);
        Assert.Equal("token", viewModel.ComAccessToken);
        Assert.Equal("secret", viewModel.ComSecret);
        Assert.True(viewModel.ComNotificationEnabled);
        Assert.Equal("https://example.com/push", viewModel.ComPushUrl);
        Assert.Equal("auth-key", viewModel.ComDeIpaasKeyAuth);
        Assert.Equal(1642112457, viewModel.ComAgentId);
        Assert.Equal(303686603505665, viewModel.ComGroupTemplateId);
        Assert.Equal(303717003821057, viewModel.ComWorkTemplateId);
        Assert.Equal("ding", viewModel.ComUserType);
        Assert.False(viewModel.SpacerValidationEnabled);
        Assert.Equal("https://spacer/api", viewModel.SpacerValidationUrl);
        Assert.Equal("8000", viewModel.SpacerValidationTimeoutMilliseconds);
        Assert.False(viewModel.SpacerValidationIgnoreServerCertificateErrors);
        Assert.Equal("-", viewModel.SpacerValidationCodeSeparator);
        Assert.Equal("9", viewModel.SpacerValidationExpectedSegmentCount);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Loaded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ShouldPersistCurrentValuesAndClearDirtyFlag()
    {
        var service = new StubUserConfigService();
        var dispatcher = new StubUiDispatcher();
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), service, new StubComNotificationService(), dispatcher, new UiBusyService(TimeSpan.Zero));
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME002";
        viewModel.PrdResponsibleWorkId = "PRD002";
        viewModel.ComAccessToken = "token-2";
        viewModel.ComSecret = "secret-2";
        viewModel.ComNotificationEnabled = false;
        viewModel.SpacerValidationEnabled = false;
        viewModel.SpacerValidationUrl = "https://spacer/save";
        viewModel.SpacerValidationTimeoutMilliseconds = "7200";
        viewModel.SpacerValidationIgnoreServerCertificateErrors = false;
        viewModel.SpacerValidationCodeSeparator = "-";
        viewModel.SpacerValidationExpectedSegmentCount = "10";

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Saved"), viewModel.StatusMessage);
        Assert.NotNull(service.LastSaved);
        Assert.Equal("ME002", service.LastSaved!.MeResponsibleWorkId);
        Assert.Equal("PRD002", service.LastSaved.PrdResponsibleWorkId);
        Assert.False(service.LastSaved.ComNotificationEnabled);
        Assert.False(service.LastSaved.SpacerValidationEnabled);
        Assert.Equal("https://spacer/save", service.LastSaved.SpacerValidationUrl);
        Assert.Equal(7200, service.LastSaved.SpacerValidationTimeoutMilliseconds);
        Assert.False(service.LastSaved.SpacerValidationIgnoreServerCertificateErrors);
        Assert.Equal("-", service.LastSaved.SpacerValidationCodeSeparator);
        Assert.Equal(10, service.LastSaved.SpacerValidationExpectedSegmentCount);
        Assert.True(dispatcher.RenderCount >= 1);
    }

    [Fact]
    public async Task InitializeAsync_WithoutSavedValue_ShouldUseComNotificationDefaultTrue()
    {
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), new StubUserConfigService(), new StubComNotificationService(), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.ComNotificationEnabled);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task TestComNotificationCommand_ShouldSaveDirtyValuesAndSendGroupAndWorkNotifications()
    {
        var service = new StubUserConfigService();
        var notificationService = new StubComNotificationService();
        var clientAppInfoService = new StubClientAppInfoService
        {
            Current = new ClientAppInfoModel
            {
                SiteCode = "S01",
                FactoryCode = "F01",
                AreaCode = "A01",
                ProcedureCode = "P01",
                EquipmentCode = "EQ01",
                ResourceNumber = "RES-TEST"
            }
        };
        var viewModel = new UserConfigViewModel(clientAppInfoService, service, notificationService, new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME003";
        viewModel.PrdResponsibleWorkId = "PRD003";
        viewModel.ComAccessToken = "token-3";
        viewModel.ComSecret = "secret-3";
        viewModel.SpacerValidationUrl = "https://spacer/test";

        await viewModel.TestComNotificationCommand.ExecuteAsync(null);

        Assert.NotNull(service.LastSaved);
        Assert.NotNull(notificationService.LastGroupUsers);
        Assert.NotNull(notificationService.LastWorkUsers);
        Assert.Equal(2, notificationService.LastGroupUsers!.Count);
        Assert.Equal(2, notificationService.LastWorkUsers!.Count);
        Assert.Contains("ME003", notificationService.LastGroupUsers!);
        Assert.Contains("PRD003", notificationService.LastGroupUsers!);
        Assert.Equal(1, notificationService.GroupCallCount);
        Assert.Equal(1, notificationService.WorkCallCount);
        Assert.NotNull(notificationService.LastGroupText);
        Assert.NotNull(notificationService.LastWorkText);
        Assert.Contains("# ", notificationService.LastGroupText!);
        Assert.Contains(LocalizedText.Get("ViewModels.ComNotificationTemplate.TestHeading"), notificationService.LastGroupText!);
        Assert.Contains("RES-TEST", notificationService.LastGroupText!);
        Assert.Contains("ME003", notificationService.LastGroupText!);
        Assert.Contains("PRD003", notificationService.LastGroupText!);
        Assert.DoesNotContain("ComNotification.Template.", notificationService.LastGroupText!);
        Assert.DoesNotContain("ViewModels.ComNotificationTemplate.", notificationService.LastGroupText!);
        Assert.Equal(notificationService.LastGroupText, notificationService.LastWorkText);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.TestSucceeded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoModel Current { get; set; } = new();

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel
            {
                Id = Current.Id,
                SiteCode = Current.SiteCode,
                FactoryCode = Current.FactoryCode,
                AreaCode = Current.AreaCode,
                ProcedureCode = Current.ProcedureCode,
                EquipmentCode = Current.EquipmentCode,
                ResourceNumber = Current.ResourceNumber,
                PlcProtocolType = Current.PlcProtocolType,
                PlcIpAddress = Current.PlcIpAddress,
                PlcPort = Current.PlcPort,
                ShutdownPointAddress = Current.ShutdownPointAddress,
                SiemensRack = Current.SiemensRack,
                SiemensSlot = Current.SiemensSlot,
                IsStringReverse = Current.IsStringReverse
            });
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUserConfigService : IUserConfigService
    {
        public UserConfig Current { get; set; } = new();

        public UserConfig? LastSaved { get; private set; }

        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new UserConfig
            {
                MeResponsibleWorkId = Current.MeResponsibleWorkId,
                PrdResponsibleWorkId = Current.PrdResponsibleWorkId,
                ComAccessToken = Current.ComAccessToken,
                ComSecret = Current.ComSecret,
                ComNotificationEnabled = Current.ComNotificationEnabled,
                ComPushUrl = Current.ComPushUrl,
                ComDeIpaasKeyAuth = Current.ComDeIpaasKeyAuth,
                ComAgentId = Current.ComAgentId,
                ComGroupTemplateId = Current.ComGroupTemplateId,
                ComWorkTemplateId = Current.ComWorkTemplateId,
                ComUserType = Current.ComUserType,
                ComTimeoutMilliseconds = Current.ComTimeoutMilliseconds,
                SpacerValidationEnabled = Current.SpacerValidationEnabled,
                SpacerValidationUrl = Current.SpacerValidationUrl,
                SpacerValidationTimeoutMilliseconds = Current.SpacerValidationTimeoutMilliseconds,
                SpacerValidationIgnoreServerCertificateErrors = Current.SpacerValidationIgnoreServerCertificateErrors,
                SpacerValidationCodeSeparator = Current.SpacerValidationCodeSeparator,
                SpacerValidationExpectedSegmentCount = Current.SpacerValidationExpectedSegmentCount
            });
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            LastSaved = new UserConfig
            {
                MeResponsibleWorkId = config.MeResponsibleWorkId,
                PrdResponsibleWorkId = config.PrdResponsibleWorkId,
                ComAccessToken = config.ComAccessToken,
                ComSecret = config.ComSecret,
                ComNotificationEnabled = config.ComNotificationEnabled,
                ComPushUrl = config.ComPushUrl,
                ComDeIpaasKeyAuth = config.ComDeIpaasKeyAuth,
                ComAgentId = config.ComAgentId,
                ComGroupTemplateId = config.ComGroupTemplateId,
                ComWorkTemplateId = config.ComWorkTemplateId,
                ComUserType = config.ComUserType,
                ComTimeoutMilliseconds = config.ComTimeoutMilliseconds,
                SpacerValidationEnabled = config.SpacerValidationEnabled,
                SpacerValidationUrl = config.SpacerValidationUrl,
                SpacerValidationTimeoutMilliseconds = config.SpacerValidationTimeoutMilliseconds,
                SpacerValidationIgnoreServerCertificateErrors = config.SpacerValidationIgnoreServerCertificateErrors,
                SpacerValidationCodeSeparator = config.SpacerValidationCodeSeparator,
                SpacerValidationExpectedSegmentCount = config.SpacerValidationExpectedSegmentCount
            };
            Current = LastSaved;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubComNotificationService : IComNotificationService
    {
        public int GroupCallCount { get; private set; }

        public int WorkCallCount { get; private set; }

        public IReadOnlyCollection<string>? LastGroupUsers { get; private set; }

        public IReadOnlyCollection<string>? LastWorkUsers { get; private set; }

        public string? LastGroupText { get; private set; }

        public string? LastWorkText { get; private set; }

        public ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            GroupCallCount++;
            LastGroupUsers = toUsers;
            LastGroupText = text;
            return ValueTask.CompletedTask;
        }

        public ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            WorkCallCount++;
            LastWorkUsers = toUsers;
            LastWorkText = text;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public int RenderCount { get; private set; }

        public void Run(Action action) => action();

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            RenderCount++;
            return Task.CompletedTask;
        }
    }
}