using System.IO;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ClientAppInfoViewModelTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly string _siteFactoryPath;
    private readonly string? _originalSiteFactoryJson;

    public ClientAppInfoViewModelTests()
    {
        _settingsDirectory = PortableDataPaths.SettingsDirectory;
        Directory.CreateDirectory(_settingsDirectory);
        _siteFactoryPath = Path.Combine(_settingsDirectory, "site-factory.json");
        _originalSiteFactoryJson = File.Exists(_siteFactoryPath) ? File.ReadAllText(_siteFactoryPath) : null;
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
    }

    [Fact]
    public async Task SaveCommand_ShouldOnlyEnableWhenFormIsDirty()
    {
        var service = new StubClientAppInfoService();
        var viewModel = new ClientAppInfoViewModel(service);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        Assert.Contains("阳极", viewModel.AreaOptions);
        Assert.Contains("阴极", viewModel.AreaOptions);
        Assert.Contains("热压/冷压", viewModel.ProcedureOptions);
        Assert.Contains("X-ray", viewModel.ProcedureOptions);
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
        Assert.Equal("客户端信息保存成功。", viewModel.StatusMessage);
        Assert.NotNull(service.LastRequest);
        Assert.Equal("阴极", service.LastRequest!.AreaCode);
        Assert.Equal("ModbusTcp", service.LastRequest.PlcProtocolType);
        Assert.False(service.LastRequest.IsStringReverse);
    }

    public void Dispose()
    {
        if (_originalSiteFactoryJson is null)
        {
            if (File.Exists(_siteFactoryPath))
            {
                File.Delete(_siteFactoryPath);
            }

            return;
        }

        File.WriteAllText(_siteFactoryPath, _originalSiteFactoryJson);
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoSaveRequest? LastRequest { get; private set; }

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel
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
                SiemensSlot = 1,
                IsStringReverse = true
            });
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
                SiemensSlot = request.SiemensSlot,
                IsStringReverse = request.IsStringReverse
            });
        }
    }
}