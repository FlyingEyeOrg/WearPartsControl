using System.IO;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
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
            appSettingsService);

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
            SiemensSlot = 1
        });

        Assert.NotNull(saved.Id);
        Assert.Equal("RES-CLIENT-01", saved.ResourceNumber);

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var entity = await verifyContext.ClientAppConfigurations.SingleAsync();
        Assert.Equal("S01", entity.SiteCode);
        Assert.Equal("EQ01", entity.EquipmentCode);

        var settings = await appSettingsService.GetAsync();
        Assert.Equal("RES-CLIENT-01", settings.ResourceNumber);
        Assert.True(settings.IsSetClientAppInfo);
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
}