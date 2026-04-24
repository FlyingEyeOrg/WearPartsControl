using System.IO;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ClientAppInfoServiceTests : IDisposable
{
    private readonly string _settingsDirectory;
    private readonly string _dbFilePath;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public ClientAppInfoServiceTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"wearparts-client-info-settings-{Guid.NewGuid():N}");
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-client-info-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(_settingsDirectory);

        _dbContextFactory = new WearPartsControlDbContextFactory($"Data Source={_dbFilePath}");
        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistConfigurationAndMarkAppSettings()
    {
        var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(_settingsDirectory), _settingsDirectory);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = new ClientAppInfoService(
            new WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories.ClientAppConfigurationRepository(dbContext),
            appSettingsService,
            new StubClientAppInfoSelectionOptionsProvider());

        var saved = await service.SaveAsync(new ClientAppInfoSaveRequest
        {
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "EQ01",
            ResourceNumber = "RES-CLIENT-01",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = "M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false
        });

        Assert.NotNull(saved.Id);
        Assert.Equal("RES-CLIENT-01", saved.ResourceNumber);

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var entity = await verifyContext.ClientAppConfigurations.SingleAsync();
        Assert.Equal("S01", entity.SiteCode);
        Assert.Equal("EQ01", entity.EquipmentCode);
        Assert.Equal(0, entity.SiemensRack);
        Assert.Equal(0, entity.SiemensSlot);
        Assert.False(entity.IsStringReverse);

        var settings = await appSettingsService.GetAsync();
        Assert.Equal("RES-CLIENT-01", settings.ResourceNumber);
        Assert.True(settings.IsSetClientAppInfo);
    }

    [Fact]
    public async Task SaveAsync_WhenTrackedChildEntitiesExist_ShouldUpdateWithoutDuplicateIdError()
    {
        var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(_settingsDirectory), _settingsDirectory);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var configurationId = Guid.NewGuid();
        dbContext.ClientAppConfigurations.Add(new WearPartsControl.Domain.Entities.ClientAppConfigurationEntity
        {
            Id = configurationId,
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "EQ01",
            ResourceNumber = "RES-CLIENT-02",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = "M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false,
            WearPartDefinitions =
            [
                new WearPartsControl.Domain.Entities.WearPartDefinitionEntity
                {
                    Id = Guid.NewGuid(),
                    ClientAppConfigurationId = configurationId,
                    ResourceNumber = "RES-CLIENT-02",
                    PartName = "Part-01",
                    InputMode = "Manual",
                    CurrentValueAddress = "DB1.0",
                    CurrentValueDataType = "Int32",
                    WarningValueAddress = "DB1.2",
                    WarningValueDataType = "Int32",
                    ShutdownValueAddress = "DB1.4",
                    ShutdownValueDataType = "Int32",
                    IsShutdown = true,
                    CodeMinLength = 1,
                    CodeMaxLength = 32,
                    LifetimeType = "计次",
                    PlcZeroClearAddress = "DB1.6",
                    BarcodeWriteAddress = "DB1.8"
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        _ = await dbContext.WearPartDefinitions.SingleAsync();

        var service = new ClientAppInfoService(
            new WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories.ClientAppConfigurationRepository(dbContext),
            appSettingsService,
            new StubClientAppInfoSelectionOptionsProvider());

        var saved = await service.SaveAsync(new ClientAppInfoSaveRequest
        {
            Id = configurationId,
            SiteCode = "S01",
            FactoryCode = "F02",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "EQ99",
            ResourceNumber = "RES-CLIENT-02",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = "127.0.0.2",
            PlcPort = 102,
            ShutdownPointAddress = "M10.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false
        });

        Assert.Equal(configurationId, saved.Id);
        Assert.Equal("F02", saved.FactoryCode);
        Assert.Equal("EQ99", saved.EquipmentCode);
    }

    public void Dispose()
    {
        TryDeleteDirectory(_settingsDirectory);
        TryDeleteFile(_dbFilePath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class StubClientAppInfoSelectionOptionsProvider : IClientAppInfoSelectionOptionsProvider
    {
        public Task<ClientAppInfoSelectionOptions> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoSelectionOptions());
        }

        public Task<string> MapAreaOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }

        public Task<string> MapProcedureOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }
    }
}