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
                MeResponsibleName = "张三",
                PrdResponsibleWorkId = "PRD001",
                PrdResponsibleName = "李四",
                ReplacementOperatorName = "王五",
                ComAccessToken = "token",
                ComSecret = "secret",
                ComNotificationEnabled = true,
                Language = "en-US",
                ComPushUrl = "https://example.com/push",
                ComDeIpaasKeyAuth = "auth-key",
                ComAgentId = 1642112457,
                ComGroupTemplateId = 303686603505665,
                ComWorkTemplateId = 303717003821057,
                ComUserType = "ding",
                ComTimeoutMilliseconds = 10000,
                SpacerValidationEnabled = false,
                SpacerValidationUrl = "https://spacer/api",
                SpacerValidationUrlRelease = "https://spacer/release",
                SpacerValidationTimeoutMilliseconds = 8000,
                SpacerValidationIgnoreServerCertificateErrors = false,
                SpacerValidationCodeSeparator = "-",
                SpacerValidationExpectedSegmentCount = 9,
                EnableCutterMesValidation = true,
                CutterMesSite = "MES-S01",
                CutterMesWsdl = "https://mes/wsdl",
                CutterMesUser = "mes-user",
                CutterMesPassword = "mes-pass"
            }
        };
        var localizationService = new StubLocalizationService();
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), service, new StubComNotificationService(), localizationService, new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        Assert.Equal("ME001", viewModel.MeResponsibleWorkId);
    Assert.Equal("张三", viewModel.MeResponsibleName);
        Assert.Equal("PRD001", viewModel.PrdResponsibleWorkId);
    Assert.Equal("李四", viewModel.PrdResponsibleName);
    Assert.Equal("王五", viewModel.ReplacementOperatorName);
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
        Assert.True(viewModel.EnableCutterMesValidation);
        Assert.Equal("MES-S01", viewModel.CutterMesSite);
        Assert.Equal("https://mes/wsdl", viewModel.CutterMesWsdl);
        Assert.Equal("mes-user", viewModel.CutterMesUser);
        Assert.Equal("mes-pass", viewModel.CutterMesPassword);
    Assert.Equal("en-US", viewModel.SelectedLanguage);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Loaded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ShouldPersistCurrentValuesAndClearDirtyFlag()
    {
        var service = new StubUserConfigService();
        var dispatcher = new StubUiDispatcher();
        var localizationService = new StubLocalizationService();
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), service, new StubComNotificationService(), localizationService, dispatcher, new UiBusyService(TimeSpan.Zero));
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME002";
        viewModel.MeResponsibleName = "赵六";
        viewModel.PrdResponsibleWorkId = "PRD002";
        viewModel.PrdResponsibleName = "孙七";
        viewModel.ReplacementOperatorName = "周八";
        viewModel.ComAccessToken = "token-2";
        viewModel.ComSecret = "secret-2";
        viewModel.ComNotificationEnabled = false;
        viewModel.SpacerValidationEnabled = false;
        viewModel.SpacerValidationTimeoutMilliseconds = "7200";
        viewModel.SpacerValidationIgnoreServerCertificateErrors = false;
        viewModel.SpacerValidationCodeSeparator = "-";
        viewModel.SpacerValidationExpectedSegmentCount = "10";
        viewModel.EnableCutterMesValidation = true;
        viewModel.CutterMesSite = "MES-S02";
        viewModel.CutterMesWsdl = "https://mes/updated";
        viewModel.CutterMesUser = "mes-user-2";
        viewModel.CutterMesPassword = "mes-pass-2";
        viewModel.SelectedLanguage = "en-US";

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Saved"), viewModel.StatusMessage);
        Assert.NotNull(service.LastSaved);
        Assert.Equal("ME002", service.LastSaved!.MeResponsibleWorkId);
        Assert.Equal("赵六", service.LastSaved.MeResponsibleName);
        Assert.Equal("PRD002", service.LastSaved.PrdResponsibleWorkId);
        Assert.Equal("孙七", service.LastSaved.PrdResponsibleName);
        Assert.Equal("周八", service.LastSaved.ReplacementOperatorName);
        Assert.Equal("en-US", service.LastSaved.Language);
        Assert.False(service.LastSaved.ComNotificationEnabled);
        Assert.False(service.LastSaved.SpacerValidationEnabled);
        Assert.Equal(UserConfig.DefaultSpacerValidationUrl, service.LastSaved.SpacerValidationUrl);
        Assert.Equal(UserConfig.DefaultSpacerValidationUrlRelease, service.LastSaved.SpacerValidationUrlRelease);
        Assert.Equal(7200, service.LastSaved.SpacerValidationTimeoutMilliseconds);
        Assert.False(service.LastSaved.SpacerValidationIgnoreServerCertificateErrors);
        Assert.Equal("-", service.LastSaved.SpacerValidationCodeSeparator);
        Assert.Equal(10, service.LastSaved.SpacerValidationExpectedSegmentCount);
        Assert.True(service.LastSaved.EnableCutterMesValidation);
        Assert.Equal("MES-S02", service.LastSaved.CutterMesSite);
        Assert.Equal("https://mes/updated", service.LastSaved.CutterMesWsdl);
        Assert.Equal("mes-user-2", service.LastSaved.CutterMesUser);
        Assert.Equal("mes-pass-2", service.LastSaved.CutterMesPassword);
        Assert.Equal("en-US", localizationService.LastCultureName);
        Assert.True(dispatcher.RenderCount >= 1);
    }

    [Fact]
    public async Task InitializeAsync_WithoutSavedValue_ShouldUseComNotificationDefaultTrue()
    {
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), new StubUserConfigService(), new StubComNotificationService(), new StubLocalizationService(), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));

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
        var viewModel = new UserConfigViewModel(clientAppInfoService, service, notificationService, new StubLocalizationService(), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME003";
        viewModel.MeResponsibleName = "张工";
        viewModel.PrdResponsibleWorkId = "PRD003";
        viewModel.PrdResponsibleName = "李工";
        viewModel.ReplacementOperatorName = "王工";
        viewModel.ComAccessToken = "token-3";
        viewModel.ComSecret = "secret-3";

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
        Assert.Contains(LocalizedText.Get("ViewModels.ComNotificationTemplate.WarningHeading"), notificationService.LastGroupText!);
        Assert.Contains("###", notificationService.LastGroupText!);
        Assert.Contains("张工(ME003)", notificationService.LastGroupText!);
        Assert.Contains("李工(PRD003)", notificationService.LastGroupText!);
        Assert.Contains("王工(###)", notificationService.LastGroupText!);
        Assert.DoesNotContain("ComNotification.Template.", notificationService.LastGroupText!);
        Assert.DoesNotContain("ViewModels.ComNotificationTemplate.", notificationService.LastGroupText!);
        Assert.Equal(notificationService.LastGroupText, notificationService.LastWorkText);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.TestSucceeded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task BuildComNotificationPreviewAsync_ShouldReturnWarningAndShutdownMarkdown()
    {
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
        var viewModel = new UserConfigViewModel(clientAppInfoService, new StubUserConfigService(), new StubComNotificationService(), new StubLocalizationService(), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME003";
        viewModel.MeResponsibleName = "张工";
        viewModel.PrdResponsibleWorkId = "PRD003";
        viewModel.PrdResponsibleName = "李工";
        viewModel.ReplacementOperatorName = "王工";

        var preview = await viewModel.BuildComNotificationPreviewAsync();

        Assert.Contains(LocalizedText.Get("ViewModels.ComNotificationTemplate.WarningHeading"), preview.Warning.Markdown);
        Assert.Contains(LocalizedText.Get("ViewModels.ComNotificationTemplate.ShutdownHeading"), preview.Shutdown.Markdown);
        Assert.Contains("**时间**：", preview.Warning.Markdown);
        Assert.Contains("- **易损件名称**：###", preview.Warning.Markdown);
        Assert.Contains("## 通知信息", preview.Warning.Markdown);
        Assert.Contains("张工(ME003)", preview.Warning.Markdown);
        Assert.Contains("李工(PRD003)", preview.Warning.Markdown);
        Assert.Contains("王工(###)", preview.Warning.Markdown);
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
                EnableCutterMesValidation = Current.EnableCutterMesValidation,
                CutterMesWsdl = Current.CutterMesWsdl,
                CutterMesUser = Current.CutterMesUser,
                CutterMesPassword = Current.CutterMesPassword,
                CutterMesSite = Current.CutterMesSite,
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
                MeResponsibleName = Current.MeResponsibleName,
                PrdResponsibleWorkId = Current.PrdResponsibleWorkId,
                PrdResponsibleName = Current.PrdResponsibleName,
                ReplacementOperatorName = Current.ReplacementOperatorName,
                Language = Current.Language,
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
                SpacerValidationUrlRelease = Current.SpacerValidationUrlRelease,
                SpacerValidationTimeoutMilliseconds = Current.SpacerValidationTimeoutMilliseconds,
                SpacerValidationIgnoreServerCertificateErrors = Current.SpacerValidationIgnoreServerCertificateErrors,
                SpacerValidationCodeSeparator = Current.SpacerValidationCodeSeparator,
                SpacerValidationExpectedSegmentCount = Current.SpacerValidationExpectedSegmentCount,
                EnableCutterMesValidation = Current.EnableCutterMesValidation,
                CutterMesSite = Current.CutterMesSite,
                CutterMesWsdl = Current.CutterMesWsdl,
                CutterMesUser = Current.CutterMesUser,
                CutterMesPassword = Current.CutterMesPassword
            });
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            LastSaved = new UserConfig
            {
                MeResponsibleWorkId = config.MeResponsibleWorkId,
                MeResponsibleName = config.MeResponsibleName,
                PrdResponsibleWorkId = config.PrdResponsibleWorkId,
                PrdResponsibleName = config.PrdResponsibleName,
                ReplacementOperatorName = config.ReplacementOperatorName,
                Language = config.Language,
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
                SpacerValidationUrlRelease = config.SpacerValidationUrlRelease,
                SpacerValidationTimeoutMilliseconds = config.SpacerValidationTimeoutMilliseconds,
                SpacerValidationIgnoreServerCertificateErrors = config.SpacerValidationIgnoreServerCertificateErrors,
                SpacerValidationCodeSeparator = config.SpacerValidationCodeSeparator,
                SpacerValidationExpectedSegmentCount = config.SpacerValidationExpectedSegmentCount,
                EnableCutterMesValidation = config.EnableCutterMesValidation,
                CutterMesSite = config.CutterMesSite,
                CutterMesWsdl = config.CutterMesWsdl,
                CutterMesUser = config.CutterMesUser,
                CutterMesPassword = config.CutterMesPassword
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

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string? LastCultureName { get; private set; }

        public string this[string name] => LocalizedText.Get(name);

        public ApplicationServices.Localization.Generated.LocalizationCatalog Catalog { get; } = new(static key => LocalizedText.Get(key));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            LastCultureName = cultureName;
            CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            return ValueTask.CompletedTask;
        }

        public System.Globalization.CultureInfo CurrentCulture { get; private set; } = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
    }
}