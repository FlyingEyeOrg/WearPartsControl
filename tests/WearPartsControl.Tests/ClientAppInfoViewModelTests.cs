using System.IO;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

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
        var viewModel = new ClientAppInfoViewModel(
            service,
            new JsonClientAppInfoSelectionOptionsProvider(new StubLocalizationService()),
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
            new UiBusyService());

        await viewModel.InitializeAsync();

        Assert.Equal("F02", viewModel.FactoryCode);
        Assert.Contains("F02", viewModel.FactoryOptions);
        Assert.False(viewModel.IsDirty);
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
}