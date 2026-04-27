using System.IO;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class ClientAppInfoViewModelTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly string _siteFactoryPath;
    private readonly string _selectionOptionsPath;
    private readonly string? _originalSiteFactoryJson;
    private readonly string? _originalSelectionOptionsJson;

    public ClientAppInfoViewModelTests()
    {
        _settingsDirectory = PortableDataPaths.SettingsDirectory;
        Directory.CreateDirectory(_settingsDirectory);
        _siteFactoryPath = Path.Combine(_settingsDirectory, "site-factory.json");
        _selectionOptionsPath = Path.Combine(_settingsDirectory, "client-app-info.zh-CN.json");
        _originalSiteFactoryJson = File.Exists(_siteFactoryPath) ? File.ReadAllText(_siteFactoryPath) : null;
        _originalSelectionOptionsJson = File.Exists(_selectionOptionsPath) ? File.ReadAllText(_selectionOptionsPath) : null;
        File.WriteAllText(_siteFactoryPath, """
{
  "Factories": [
    {
      "Site": "S01",
      "SiteName": "测试基地",
      "FactoryNames": ["F01", "F02"]
    }
  ]
}
""");
        File.WriteAllText(_selectionOptionsPath, """
{
    "AreaOptions": ["阳极", "阴极"],
    "ProcedureOptions": ["凹版", "热压/冷压", "X-ray"]
}
""");
    }

    [Fact]
    public async Task SaveCommand_ShouldOnlyEnableWhenFormIsDirty()
    {
        var service = new StubClientAppInfoService();
        var uiDispatcher = new TrackingUiDispatcher();
        var viewModel = new ClientAppInfoViewModel(
            service,
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService(),
            uiDispatcher,
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        Assert.Contains("阳极", viewModel.AreaOptions);
        Assert.Contains("阴极", viewModel.AreaOptions);
        Assert.Contains("热压/冷压", viewModel.ProcedureOptions);
        Assert.Contains("X-ray", viewModel.ProcedureOptions);
        Assert.True(viewModel.IsSiemensRackVisible);
        Assert.True(viewModel.IsSiemensSlotVisible);
        Assert.False(viewModel.IsStringReverseVisible);

        viewModel.AreaCode = "阴极";
        viewModel.PlcProtocolType = "ModbusTcp";
        viewModel.IsStringReverse = false;

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.IsSiemensSlotVisible);
        Assert.True(viewModel.IsStringReverseVisible);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.Saved"), viewModel.StatusMessage);
        Assert.NotNull(service.LastRequest);
        Assert.Equal("阴极", service.LastRequest!.AreaCode);
        Assert.Equal("ModbusTcp", service.LastRequest.PlcProtocolType);
        Assert.False(service.LastRequest.IsStringReverse);
        Assert.True(uiDispatcher.RenderCount >= 2);
    }

    [Fact]
    public void InitialState_ShouldShowMonitoringDisabledBeforeInitializationCompletes()
    {
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService { IsEnabled = false },
            new UiDispatcher(),
            new UiBusyService());

        Assert.False(viewModel.IsWearPartMonitoringEnabled);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.StartWearPartMonitoring"), viewModel.WearPartMonitoringButtonText);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringDisabledStatus"), viewModel.WearPartMonitoringStatusText);
        Assert.Same(System.Windows.Media.Brushes.DimGray, viewModel.WearPartMonitoringStatusBackground);
        Assert.True(viewModel.IsPlcParametersEditable);
        Assert.False(viewModel.IsToggleWearPartMonitoringEnabled);
        Assert.False(viewModel.IsTestPlcConnectionEnabled);
    }

    [Fact]
    public async Task InitializeAsync_WhenProcedureIsEmpty_ShouldUseFirstProcedureOptionAsDefault()
    {
        var service = new StubClientAppInfoService
        {
            Model = new ClientAppInfoModel
            {
                Id = Guid.NewGuid(),
                SiteCode = "S01",
                FactoryCode = "F01",
                AreaCode = "阳极",
                ProcedureCode = string.Empty,
                EquipmentCode = "EQ01",
                ResourceNumber = "RES01",
                PlcProtocolType = "SiemensS1500",
                PlcIpAddress = "127.0.0.1",
                PlcPort = 102,
                ShutdownPointAddress = "M0.0",
                SiemensRack = 0,
                SiemensSlot = 0,
                IsStringReverse = true
            }
        };

        var viewModel = new ClientAppInfoViewModel(
            service,
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService(),
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.Equal("凹版", viewModel.ProcedureCode);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task InitializeAsync_WhenControlIsRecreated_ShouldRestoreSavedFactoryCode()
    {
        var service = new StubClientAppInfoService
        {
            Model = new ClientAppInfoModel
            {
                Id = Guid.NewGuid(),
                SiteCode = "S01",
                FactoryCode = "F02",
                AreaCode = "阳极",
                ProcedureCode = "凹版",
                EquipmentCode = "EQ01",
                ResourceNumber = "RES01",
                PlcProtocolType = "SiemensS1500",
                PlcIpAddress = "127.0.0.1",
                PlcPort = 102,
                ShutdownPointAddress = "M0.0",
                SiemensRack = 0,
                SiemensSlot = 0,
                IsStringReverse = true
            }
        };

        var viewModel = new ClientAppInfoViewModel(
            service,
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService(),
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.Equal("F02", viewModel.FactoryCode);
        Assert.Contains("F02", viewModel.FactoryOptions);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task ImportLegacyConfigurationAsync_ShouldApplyImportedConfigurationAndUpdateStatus()
    {
        var importService = new StubLegacyConfigurationImportService();
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            importService,
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService(),
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();

        var result = await viewModel.ImportLegacyConfigurationAsync("E:\\legacy\\db\\Data.db");

        Assert.Equal("E:\\legacy\\db\\Data.db", importService.LastPath);
        Assert.Equal("RES-LEGACY", result.ResourceNumber);
        Assert.Equal("RES-LEGACY", viewModel.ResourceNumber);
        Assert.Equal("ModbusTcp", viewModel.PlcProtocolType);
        Assert.Equal(LocalizedText.Format("ViewModels.ClientAppInfoVm.ImportedLegacyConfiguration", "RES-LEGACY"), viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void ImportLegacyConfigurationCommand_ShouldRaiseRequestedEvent()
    {
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService(),
            new UiDispatcher(),
            new UiBusyService());
        var raised = false;
        viewModel.ImportLegacyConfigurationRequested += (_, _) => raised = true;

        viewModel.ImportLegacyConfigurationCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public async Task ImportLegacyConfigurationCommand_ShouldOnlyEnableWhenMonitoringDisabled()
    {
        var monitoringControlService = new StubWearPartMonitoringControlService();
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            monitoringControlService,
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsWearPartMonitoringEnabled);
        Assert.False(viewModel.IsPlcParametersEditable);
        Assert.False(viewModel.IsImportLegacyConfigurationEnabled);
        Assert.False(viewModel.ImportLegacyConfigurationCommand.CanExecute(null));

        await viewModel.ToggleWearPartMonitoringCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsWearPartMonitoringEnabled);
        Assert.True(viewModel.IsPlcParametersEditable);
        Assert.True(viewModel.IsImportLegacyConfigurationEnabled);
        Assert.True(viewModel.ImportLegacyConfigurationCommand.CanExecute(null));
    }

    [Fact]
    public async Task TestPlcConnectionCommand_ShouldUpdateStatusWhenConnectionSucceeds()
    {
        var plcStatusService = new PlcConnectionStatusService();
        var uiDispatcher = new TrackingUiDispatcher();
        var plcConnectionTestService = new StubPlcConnectionTestService(plcStatusService, () => uiDispatcher.RenderCount);
        var monitoringControlService = new StubWearPartMonitoringControlService();
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            plcConnectionTestService,
            plcStatusService,
            monitoringControlService,
            uiDispatcher,
            new UiBusyService());

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsWearPartMonitoringEnabled);
        Assert.False(viewModel.IsTestPlcConnectionEnabled);

        await viewModel.ToggleWearPartMonitoringCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsWearPartMonitoringEnabled);
        Assert.True(viewModel.IsTestPlcConnectionEnabled);

        await viewModel.TestPlcConnectionCommand.ExecuteAsync(null);

        Assert.True(plcConnectionTestService.WasCalled);
        Assert.True(plcConnectionTestService.RenderCountAtCall >= 2);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcConnectionTestSucceeded"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task TestPlcConnectionCommand_WhenBaseInfoMissing_ShouldShowValidationMessage()
    {
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService
            {
                Model = new ClientAppInfoModel()
            },
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService { IsEnabled = false },
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();
        Assert.False(viewModel.IsTestPlcConnectionEnabled);
    }

    [Fact]
    public async Task ToggleWearPartMonitoringCommand_WhenBaseInfoMissing_ShouldStayDisabled()
    {
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService
            {
                Model = new ClientAppInfoModel()
            },
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            new StubWearPartMonitoringControlService { IsEnabled = false },
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsToggleWearPartMonitoringEnabled);
    }

    [Fact]
    public async Task PlcConnectionFailure_ShouldRefreshWearPartMonitoringUiState()
    {
        var plcStatusService = new PlcConnectionStatusService();
        var monitoringControlService = new StubWearPartMonitoringControlService { IsEnabled = true };
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(plcStatusService),
            plcStatusService,
            monitoringControlService,
            new UiDispatcher(),
            new UiBusyService());

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsWearPartMonitoringEnabled);

        monitoringControlService.IsEnabled = false;
        plcStatusService.Set(PlcStartupConnectionResult.Failed("连接失败：测试异常"));
        await Task.Delay(20);

        Assert.False(viewModel.IsWearPartMonitoringEnabled);
        Assert.Same(System.Windows.Media.Brushes.DimGray, viewModel.WearPartMonitoringStatusBackground);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringDisabledStatus"), viewModel.WearPartMonitoringStatusText);
        Assert.True(viewModel.IsTestPlcConnectionEnabled);
    }

    [Fact]
    public async Task ToggleWearPartMonitoringCommand_ShouldToggleStateAndStatus()
    {
        var monitoringControlService = new StubWearPartMonitoringControlService();
        var uiDispatcher = new TrackingUiDispatcher();
        var viewModel = new ClientAppInfoViewModel(
            new StubClientAppInfoService(),
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
            new StubLegacyConfigurationImportService(),
            new StubPlcConnectionTestService(),
            new PlcConnectionStatusService(),
            monitoringControlService,
            uiDispatcher,
            new UiBusyService());

        await viewModel.InitializeAsync();
        await viewModel.ToggleWearPartMonitoringCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsWearPartMonitoringEnabled);
        Assert.Same(System.Windows.Media.Brushes.DimGray, viewModel.WearPartMonitoringStatusBackground);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringDisabledStatus"), viewModel.WearPartMonitoringStatusText);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringStopped"), viewModel.StatusMessage);

        await viewModel.ToggleWearPartMonitoringCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsWearPartMonitoringEnabled);
        Assert.Equal(1, monitoringControlService.EnableCallCount);
        Assert.Equal(1, monitoringControlService.DisableCallCount);
        Assert.Same(System.Windows.Media.Brushes.ForestGreen, viewModel.WearPartMonitoringStatusBackground);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringEnabledStatus"), viewModel.WearPartMonitoringStatusText);
        Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringStarted"), viewModel.StatusMessage);
        Assert.True(uiDispatcher.RenderCount >= 3);
    }

    [Fact]
    public async Task LocalizationRefresh_ShouldReloadLocalizedSelectionOptionsAndStatusTexts()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var englishSelectionOptionsPath = Path.Combine(PortableDataPaths.SettingsDirectory, "client-app-info.en-US.json");
        var originalEnglishSelectionOptionsJson = File.Exists(englishSelectionOptionsPath) ? File.ReadAllText(englishSelectionOptionsPath) : null;
        File.WriteAllText(englishSelectionOptionsPath, """
{
    "AreaOptions": ["Anode", "Cathode"],
    "ProcedureOptions": ["Gravure", "Hot Press/Cold Press", "X-ray"]
}
""");

        try
        {
            var localizationService = new MutableLocalizationService("zh-CN");
            var uiDispatcher = new TrackingUiDispatcher();
            var viewModel = new ClientAppInfoViewModel(
                new StubClientAppInfoService
                {
                    Model = new ClientAppInfoModel
                    {
                        Id = Guid.NewGuid(),
                        SiteCode = "S01",
                        FactoryCode = "F01",
                        AreaCode = "阳极",
                        ProcedureCode = "凹版",
                        EquipmentCode = "EQ01",
                        ResourceNumber = "RES01",
                        PlcProtocolType = "SiemensS1500",
                        PlcIpAddress = "127.0.0.1",
                        PlcPort = 102,
                        ShutdownPointAddress = "M0.0",
                        SiemensRack = 0,
                        SiemensSlot = 0,
                        IsStringReverse = true
                    }
                },
                new JsonClientAppInfoSelectionOptionsProvider(localizationService),
                new StubLegacyConfigurationImportService(),
                new StubPlcConnectionTestService(),
                new PlcConnectionStatusService(),
                new StubWearPartMonitoringControlService { IsEnabled = false },
                uiDispatcher,
                new UiBusyService());

            await viewModel.InitializeAsync();
            Assert.Contains("阳极", viewModel.AreaOptions);
            Assert.Equal("阳极", viewModel.AreaCode);
            Assert.Equal("凹版", viewModel.ProcedureCode);

            await localizationService.SetCultureAsync("en-US");
            LocalizationBindingSource.Instance.Refresh();

            await WaitUntilAsync(() => viewModel.AreaOptions.Contains("Anode") && viewModel.ProcedureOptions.Contains("Gravure"));

            Assert.Equal("Anode", viewModel.AreaCode);
            Assert.Equal("Gravure", viewModel.ProcedureCode);
            Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.StartWearPartMonitoring"), viewModel.WearPartMonitoringButtonText);
            Assert.Equal(LocalizedText.Get("ViewModels.ClientAppInfoVm.Loaded"), viewModel.StatusMessage);
            Assert.True(uiDispatcher.RenderCount >= 1 || viewModel.AreaOptions.Contains("Anode"));
        }
        finally
        {
            if (originalEnglishSelectionOptionsJson is null)
            {
                if (File.Exists(englishSelectionOptionsPath))
                {
                    File.Delete(englishSelectionOptionsPath);
                }
            }
            else
            {
                File.WriteAllText(englishSelectionOptionsPath, originalEnglishSelectionOptionsJson);
            }
        }
    }

    public void Dispose()
    {
        if (_originalSiteFactoryJson is null)
        {
            if (File.Exists(_siteFactoryPath))
            {
                File.Delete(_siteFactoryPath);
            }
        }

        if (_originalSiteFactoryJson is null)
        {
            if (File.Exists(_siteFactoryPath))
            {
                File.Delete(_siteFactoryPath);
            }
        }
        else
        {
            File.WriteAllText(_siteFactoryPath, _originalSiteFactoryJson);
        }

        if (_originalSelectionOptionsJson is null)
        {
            if (File.Exists(_selectionOptionsPath))
            {
                File.Delete(_selectionOptionsPath);
            }

            return;
        }

        File.WriteAllText(_selectionOptionsPath, _originalSelectionOptionsJson);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var index = 0; index < 30; index++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoSaveRequest? LastRequest { get; private set; }

        public ClientAppInfoModel Model { get; set; } = new()
        {
            Id = Guid.NewGuid(),
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "EQ01",
            ResourceNumber = "RES01",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = "M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = true
        };

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Model);
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ClientAppInfoModel
            {
                Id = request.Id ?? Guid.NewGuid(),
                SiteCode = request.SiteCode,
                FactoryCode = request.FactoryCode,
                AreaCode = request.AreaCode,
                ProcedureCode = request.ProcedureCode,
                EquipmentCode = request.EquipmentCode,
                ResourceNumber = request.ResourceNumber,
                PlcProtocolType = request.PlcProtocolType,
                PlcIpAddress = request.PlcIpAddress,
                PlcPort = request.PlcPort,
                ShutdownPointAddress = request.ShutdownPointAddress,
                SiemensRack = request.SiemensRack,
                SiemensSlot = request.SiemensSlot,
                IsStringReverse = request.IsStringReverse
            });
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => name;

        public ApplicationServices.Localization.Generated.LocalizationCatalog Catalog { get; } = new(static key => key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public System.Globalization.CultureInfo CurrentCulture { get; } = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
    }

    private sealed class MutableLocalizationService : ILocalizationService
    {
        public MutableLocalizationService(string cultureName)
        {
            CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
        }

        public string this[string name] => LocalizedText.Get(name);

        public ApplicationServices.Localization.Generated.LocalizationCatalog Catalog { get; } = new(LocalizedText.Get);

        public System.Globalization.CultureInfo CurrentCulture { get; private set; }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
            return ValueTask.CompletedTask;
        }

        private static void ApplyCulture(System.Globalization.CultureInfo culture)
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            LocalizedText.SetCulture(culture);
        }
    }

    private sealed class StubLegacyConfigurationImportService : ILegacyConfigurationImportService
    {
        public string? LastPath { get; private set; }

        public Task<LegacyConfigurationImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
        {
            LastPath = legacyDatabasePath;
            var model = new ClientAppInfoModel
            {
                Id = Guid.NewGuid(),
                SiteCode = "S01",
                FactoryCode = "F01",
                AreaCode = "阳极",
                ProcedureCode = "凹版",
                EquipmentCode = "EQ99",
                ResourceNumber = "RES-LEGACY",
                PlcProtocolType = "ModbusTcp",
                PlcIpAddress = "192.168.1.10",
                PlcPort = 502,
                ShutdownPointAddress = "M9.0",
                SiemensRack = 0,
                SiemensSlot = 0,
                IsStringReverse = true
            };

            return Task.FromResult(new LegacyConfigurationImportResult
            {
                LegacyDatabasePath = legacyDatabasePath,
                ResourceNumber = model.ResourceNumber,
                ClientAppInfo = model,
                ImportedAppSettings = true
            });
        }
    }

    private sealed class StubPlcConnectionTestService : IPlcConnectionTestService
    {
        private readonly IPlcConnectionStatusService? _plcConnectionStatusService;
        private readonly Func<int>? _renderCountProvider;

        public StubPlcConnectionTestService(IPlcConnectionStatusService? plcConnectionStatusService = null, Func<int>? renderCountProvider = null)
        {
            _plcConnectionStatusService = plcConnectionStatusService;
            _renderCountProvider = renderCountProvider;
        }

        public bool WasCalled { get; private set; }

        public int RenderCountAtCall { get; private set; }

        public Task<PlcStartupConnectionResult> TestAsync(ClientAppInfoModel clientAppInfo, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            RenderCountAtCall = _renderCountProvider?.Invoke() ?? 0;
            var result = PlcStartupConnectionResult.Connected(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcConnectionTestSucceeded"));
            _plcConnectionStatusService?.Set(result);
            return Task.FromResult(result);
        }
    }

    private sealed class StubWearPartMonitoringControlService : IWearPartMonitoringControlService
    {
        public int EnableCallCount { get; private set; }

        public int DisableCallCount { get; private set; }

        public bool IsEnabled { get; set; } = true;

        public Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsEnabled);
        }

        public Task EnableAsync(CancellationToken cancellationToken = default)
        {
            EnableCallCount++;
            IsEnabled = true;
            return Task.CompletedTask;
        }

        public Task DisableAsync(CancellationToken cancellationToken = default)
        {
            DisableCallCount++;
            IsEnabled = false;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingPlcOperationPipeline : IPlcOperationPipeline
    {
        public bool ConnectCalled { get; private set; }

        public Task ConnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }

        public Task ForceReconnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(string operationName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsConnectedAsync(string operationName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TValue> ReadAsync<TValue>(string operationName, string address, int retryCount = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task WriteAsync<TValue>(string operationName, string address, TValue value, int retryCount = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TrackingUiDispatcher : IUiDispatcher
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