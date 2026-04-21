using System.Globalization;
using System.IO;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Domain.Services;
using WearPartsControl.Exceptions;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartOperationalServicesTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public WearPartOperationalServicesTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-ops-{Guid.NewGuid():N}.db");
        _dbContextFactory = new WearPartsControlDbContextFactory($"Data Source={_dbFilePath}");

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ReplaceByScanAsync_WhenAccessLevelInsufficient_ShouldThrowAuthorizationException()
    {
        var seeded = await SeedAsync("R-OPS-01", "M0.0");
        var currentUserAccessor = CreateCurrentUserAccessor(accessLevel: 0);
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 12);
        plcService.SetValue("DB1.1", 20);
        plcService.SetValue("DB1.2", 30);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateReplacementService(dbContext, currentUserAccessor, plcService);

        await Assert.ThrowsAsync<AuthorizationException>(() => service.ReplaceByScanAsync(new WearPartReplacementRequest
        {
            WearPartDefinitionId = seeded.DefinitionId,
            NewBarcode = "BARCODE-0001",
            ReplacementReason = "寿命到期正常更换"
        }));
    }

    [Fact]
    public async Task ReplaceByScanAsync_ShouldWritePlcAndPersistRecord()
    {
        var seeded = await SeedAsync("R-OPS-02", "M0.1");
        var currentUserAccessor = CreateCurrentUserAccessor(accessLevel: 1);
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 30);
        plcService.SetValue("DB1.1", 20);
        plcService.SetValue("DB1.2", 30);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateReplacementService(dbContext, currentUserAccessor, plcService);

        var result = await service.ReplaceByScanAsync(new WearPartReplacementRequest
        {
            WearPartDefinitionId = seeded.DefinitionId,
            NewBarcode = "BARCODE-0002",
            ReplacementReason = WearPartReplacementReason.Normal,
            ReplacementMessage = "扫码更换"
        });

        Assert.Equal("BARCODE-0002", result.NewBarcode);
        Assert.Contains(plcService.Writes, x => x.Address == "DB1.3" && Equals(x.Value, true));
        Assert.Contains(plcService.Writes, x => x.Address == "DB1.3" && Equals(x.Value, false));
        Assert.Contains(plcService.Writes, x => x.Address == "DB1.4" && Equals(x.Value, "BARCODE-0002"));
        Assert.Contains(plcService.Writes, x => x.Address == "M0.1" && Equals(x.Value, false));

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new WearPartReplacementRecordRepository(verifyContext);
        var records = await repository.ListByClientAppConfigurationAsync(seeded.BasicConfigurationId);
        Assert.Single(records);
        Assert.Equal(WearPartReplacementReason.Normal, records[0].ReplacementReason);
    }

    [Fact]
    public async Task ReplaceByScanAsync_WhenLifetimeNotReached_ShouldThrowUserFriendlyException()
    {
        var seeded = await SeedAsync("R-OPS-05", "M0.5");
        var currentUserAccessor = CreateCurrentUserAccessor(accessLevel: 1);
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 12);
        plcService.SetValue("DB1.1", 20);
        plcService.SetValue("DB1.2", 30);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateReplacementService(dbContext, currentUserAccessor, plcService);

        var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => service.ReplaceByScanAsync(new WearPartReplacementRequest
        {
            WearPartDefinitionId = seeded.DefinitionId,
            NewBarcode = "BARCODE-0005",
            ReplacementReason = WearPartReplacementReason.Normal
        }));

        Assert.Equal(LocalizedText.Get("Services.WearPartReplacement.LifetimeNotReached"), exception.Message);
    }

    [Fact]
    public async Task ReplaceByScanAsync_WhenChangePositionWithoutMe_ShouldThrowAuthorizationException()
    {
        var seeded = await SeedAsync("R-OPS-06", "M0.6");
        var currentUserAccessor = CreateCurrentUserAccessor(accessLevel: 1);
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 30);
        plcService.SetValue("DB1.1", 20);
        plcService.SetValue("DB1.2", 30);

        await using var seedContext = await _dbContextFactory.CreateDbContextAsync();
        seedContext.WearPartReplacementRecords.Add(new WearPartReplacementRecordEntity
        {
            ClientAppConfigurationId = seeded.BasicConfigurationId,
            WearPartDefinitionId = seeded.DefinitionId,
            SiteCode = "S01",
            PartName = "刀具A",
            OldBarcode = "OLD-0001",
            NewBarcode = "BARCODE-0006",
            CurrentValue = "30",
            WarningValue = "20",
            ShutdownValue = "30",
            OperatorWorkNumber = "WORK-OPS",
            OperatorUserName = "WORK-OPS",
            ReplacementReason = WearPartReplacementReason.Normal,
            ReplacementMessage = string.Empty,
            ReplacedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await seedContext.SaveChangesAsync();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateReplacementService(dbContext, currentUserAccessor, plcService);

        await Assert.ThrowsAsync<AuthorizationException>(() => service.ReplaceByScanAsync(new WearPartReplacementRequest
        {
            WearPartDefinitionId = seeded.DefinitionId,
            NewBarcode = "BARCODE-0006",
            ReplacementReason = WearPartReplacementReason.ChangePosition
        }));
    }

    [Fact]
    public async Task ReplaceByScanAsync_WhenBarcodeRemovedByProcessDamage_ShouldAllowReuseAndWritePreviousCurrentValue()
    {
        var seeded = await SeedAsync("R-OPS-07", "M0.7", plcZeroClearAddress: "######");
        var currentUserAccessor = CreateCurrentUserAccessor(accessLevel: 1);
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 5);
        plcService.SetValue("DB1.1", 20);
        plcService.SetValue("DB1.2", 30);

        await using var seedContext = await _dbContextFactory.CreateDbContextAsync();
        seedContext.WearPartReplacementRecords.AddRange(
            new WearPartReplacementRecordEntity
            {
                ClientAppConfigurationId = seeded.BasicConfigurationId,
                WearPartDefinitionId = seeded.DefinitionId,
                SiteCode = "S01",
                PartName = "刀具A",
                OldBarcode = null,
                NewBarcode = "BARCODE-REUSE",
                CurrentValue = "18",
                WarningValue = "20",
                ShutdownValue = "30",
                OperatorWorkNumber = "WORK-OPS",
                OperatorUserName = "WORK-OPS",
                ReplacementReason = WearPartReplacementReason.Normal,
                ReplacementMessage = string.Empty,
                ReplacedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new WearPartReplacementRecordEntity
            {
                ClientAppConfigurationId = seeded.BasicConfigurationId,
                WearPartDefinitionId = seeded.DefinitionId,
                SiteCode = "S01",
                PartName = "刀具A",
                OldBarcode = "BARCODE-REUSE",
                NewBarcode = "BARCODE-NEWER",
                CurrentValue = "18",
                WarningValue = "20",
                ShutdownValue = "30",
                OperatorWorkNumber = "WORK-OPS",
                OperatorUserName = "WORK-OPS",
                ReplacementReason = WearPartReplacementReason.ProcessDamage,
                ReplacementMessage = string.Empty,
                ReplacedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        await seedContext.SaveChangesAsync();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateReplacementService(dbContext, currentUserAccessor, plcService);

        var result = await service.ReplaceByScanAsync(new WearPartReplacementRequest
        {
            WearPartDefinitionId = seeded.DefinitionId,
            NewBarcode = "BARCODE-REUSE",
            ReplacementReason = WearPartReplacementReason.ProcessDamage
        });

        Assert.Equal("BARCODE-REUSE", result.NewBarcode);
        Assert.Contains(plcService.Writes, x => x.Address == "DB1.0" && Equals(x.Value, 18));
    }

    [Fact]
    public async Task MonitorByResourceNumberAsync_WhenWarningExceeded_ShouldPersistRecordAndNotifyGroup()
    {
        var seeded = await SeedAsync("R-OPS-03", "M0.2");
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 15);
        plcService.SetValue("DB1.1", 10);
        plcService.SetValue("DB1.2", 20);
        var notificationService = new FakeComNotificationService();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateMonitorService(dbContext, plcService, notificationService);

        var results = await service.MonitorByResourceNumberAsync("R-OPS-03");

        Assert.Single(results);
        Assert.Equal(WearPartMonitorStatus.Warning, results[0].Status);
        Assert.True(results[0].NotificationTriggered);
        Assert.Single(notificationService.GroupNotifications);
        Assert.Empty(notificationService.WorkNotifications);

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new ExceedLimitRecordRepository(verifyContext);
        var records = await repository.ListByClientAppConfigurationAsync(seeded.BasicConfigurationId);
        Assert.Single(records);
        Assert.Equal("Warning", records[0].Severity);
    }

    [Fact]
    public async Task MonitorByResourceNumberAsync_WhenShutdownExceeded_ShouldWriteShutdownSignal()
    {
        var seeded = await SeedAsync("R-OPS-04", "!M0.3");
        var plcService = new FakePlcService();
        plcService.SetValue("DB1.0", 25);
        plcService.SetValue("DB1.1", 10);
        plcService.SetValue("DB1.2", 20);
        var notificationService = new FakeComNotificationService();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var service = CreateMonitorService(dbContext, plcService, notificationService);

        var results = await service.MonitorByResourceNumberAsync("R-OPS-04");

        Assert.Single(results);
        Assert.Equal(WearPartMonitorStatus.Shutdown, results[0].Status);
        Assert.Contains(plcService.Writes, x => x.Address == "M0.3" && Equals(x.Value, false));
        Assert.Single(notificationService.WorkNotifications);
    }

    private async Task<(Guid BasicConfigurationId, Guid DefinitionId)> SeedAsync(string resourceNumber, string shutdownPointAddress, string plcZeroClearAddress = "DB1.3")
    {
        var basicConfigurationId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.ClientAppConfigurations.Add(new ClientAppConfigurationEntity
        {
            Id = basicConfigurationId,
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "E01",
            ResourceNumber = resourceNumber,
            PlcProtocolType = "S7",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = shutdownPointAddress,
            SiemensSlot = 1,
            IsStringReverse = true
        });

        dbContext.WearPartDefinitions.Add(new WearPartDefinitionEntity
        {
            Id = definitionId,
            ClientAppConfigurationId = basicConfigurationId,
            ResourceNumber = resourceNumber,
            PartName = "刀具A",
            InputMode = "Barcode",
            CurrentValueAddress = "DB1.0",
            CurrentValueDataType = "Int32",
            WarningValueAddress = "DB1.1",
            WarningValueDataType = "Int32",
            ShutdownValueAddress = "DB1.2",
            ShutdownValueDataType = "Int32",
            IsShutdown = true,
            CodeMinLength = 8,
            CodeMaxLength = 32,
            LifetimeType = "Count",
            PlcZeroClearAddress = plcZeroClearAddress,
            BarcodeWriteAddress = "DB1.4"
        });

        await dbContext.SaveChangesAsync();
        return (basicConfigurationId, definitionId);
    }

    private static CurrentUserAccessor CreateCurrentUserAccessor(int accessLevel)
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new WearPartsControl.ApplicationServices.LoginService.MhrUser
        {
            CardId = "CARD-OPS",
            WorkId = "WORK-OPS",
            AccessLevel = accessLevel
        });
        return accessor;
    }

    private static WearPartReplacementService CreateReplacementService(WearPartsControlDbContext dbContext, ICurrentUserAccessor currentUserAccessor, IPlcService plcService)
    {
        return new WearPartReplacementService(
            currentUserAccessor,
            new ClientAppConfigurationRepository(dbContext),
            new WearPartRepository(dbContext, new WearPartDefinitionDomainService()),
            new WearPartReplacementRecordRepository(dbContext),
            plcService,
            [
                new BarcodeLengthReplacementGuard(),
                new BarcodeReuseReplacementGuard(new WearPartReplacementRecordRepository(dbContext)),
                new LifetimeReachedReplacementGuard(),
                new ChangePositionReplacementGuard()
            ]);
    }

    private static WearPartMonitorService CreateMonitorService(WearPartsControlDbContext dbContext, IPlcService plcService, IComNotificationService notificationService)
    {
        return new WearPartMonitorService(
            new CurrentUserAccessor(),
            new ClientAppConfigurationRepository(dbContext),
            new WearPartRepository(dbContext, new WearPartDefinitionDomainService()),
            new ExceedLimitRecordRepository(dbContext),
            plcService,
            notificationService);
    }

    public void Dispose()
    {
        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            dbContext.Database.EnsureDeleted();
        }

        try
        {
            if (File.Exists(_dbFilePath))
            {
                File.Delete(_dbFilePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakePlcService : IPlcService
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public bool IsConnected { get; private set; }

        public List<(string Address, object? Value)> Writes { get; } = new();

        public void SetValue(string address, object value)
        {
            _values[address] = value;
        }

        public void Connect(PlcConnectionOptions options)
        {
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public TValue Read<TValue>(string address, int retryCount = 1)
        {
            var value = _values[address];
            if (value is TValue matched)
            {
                return matched;
            }

            return (TValue)Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture);
        }

        public void Write<TValue>(string address, TValue value, int retryCount = 1)
        {
            Writes.Add((address, value));
            _values[address] = value!;
        }
    }

    private sealed class FakeComNotificationService : IComNotificationService
    {
        public List<string> GroupNotifications { get; } = new();

        public List<string> WorkNotifications { get; } = new();

        public ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            GroupNotifications.Add($"{title}:{text}");
            return ValueTask.CompletedTask;
        }

        public ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            WorkNotifications.Add($"{title}:{text}");
            return ValueTask.CompletedTask;
        }
    }
}