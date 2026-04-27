using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LegacyDatabaseImportServiceTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _legacyRoot;
    private readonly string _legacyDbPath;
    private readonly string _targetDbPath;
    private readonly TypeJsonSaveInfoStore _saveInfoStore;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public LegacyDatabaseImportServiceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"wearparts-import-tests-{Guid.NewGuid():N}");
        _legacyRoot = Path.Combine(_workspace, "legacy");
        Directory.CreateDirectory(Path.Combine(_legacyRoot, "db"));
        Directory.CreateDirectory(Path.Combine(_legacyRoot, "Json"));
        _legacyDbPath = Path.Combine(_legacyRoot, "db", "Data.db");
        _targetDbPath = Path.Combine(Path.GetTempPath(), $"wearparts-import-{Guid.NewGuid():N}.db");
        _saveInfoStore = new TypeJsonSaveInfoStore(Path.Combine(_workspace, "settings"));
        _dbContextFactory = new WearPartsControlDbContextFactory($"Data Source={_targetDbPath}");

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        CreateLegacyDatabase();
    }

    [Fact]
    public async Task ImportAsync_ShouldMapLegacySqliteData()
    {
        await File.WriteAllTextAsync(Path.Combine(_legacyRoot, "Json", "ToolChangeSaveInfo.json"), """
    {"ToolChangeItems":[{"PartId":"part-01","SelectedValue":"TOOL-05"}]}
    """);

        var service = new LegacyDatabaseImportService(_dbContextFactory, _saveInfoStore);

        var result = await service.ImportAsync(_legacyDbPath);

        Assert.Equal(1, result.ImportedClientConfigurations);
        Assert.Equal(1, result.ImportedWearPartDefinitions);
        Assert.Equal(1, result.ImportedReplacementRecords);
        Assert.Equal(1, result.ImportedExceedLimitRecords);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var configuration = await dbContext.ClientAppConfigurations.SingleAsync();
        var definition = await dbContext.WearPartDefinitions.SingleAsync();
        var replacement = await dbContext.WearPartReplacementRecords.SingleAsync();
        var exceed = await dbContext.ExceedLimitRecords.SingleAsync();

        Assert.Equal("RES-IMPORT-01", configuration.ResourceNumber);
        Assert.Equal("刀具A", definition.PartName);
        Assert.Null(definition.ToolChangeId);
        Assert.Equal("BARCODE-OLD", replacement.CurrentBarcode);
        Assert.Equal("BARCODE-NEW", replacement.NewBarcode);
        Assert.Equal("12", replacement.CurrentValue);
        Assert.Equal("Shutdown", exceed.Severity);

        var toolSelection = await _saveInfoStore.ReadAsync<ToolChangeSelectionSaveInfo>();
        Assert.Single(toolSelection.Items);
        Assert.Equal(definition.Id, toolSelection.Items[0].WearPartDefinitionId);
        Assert.Equal("TOOL-05", toolSelection.Items[0].SelectedToolCode);
        Assert.Equal(["TOOL-05"], toolSelection.RecentToolCodes);
    }

    [Fact]
    public async Task ImportWearPartDefinitionsAsync_ShouldImportDefinitionsIntoSpecifiedClientConfiguration()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var configuration = new Domain.Entities.ClientAppConfigurationEntity
        {
            Id = Guid.NewGuid(),
            ResourceNumber = "RES-TARGET-01",
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "E01",
            PlcProtocolType = "S7",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = "!M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false
        };
        dbContext.ClientAppConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync();

        var service = new LegacyDatabaseImportService(_dbContextFactory, _saveInfoStore);

        var result = await service.ImportWearPartDefinitionsAsync(_legacyDbPath, configuration.Id, configuration.ResourceNumber);

        Assert.Equal(1, result.ImportedWearPartDefinitions);
        Assert.Equal(0, result.UpdatedWearPartDefinitions);

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var definition = await verifyContext.WearPartDefinitions.SingleAsync(x => x.ClientAppConfigurationId == configuration.Id);
        Assert.Equal(configuration.ResourceNumber, definition.ResourceNumber);
        Assert.Equal("刀具A", definition.PartName);
        Assert.Equal("Scanner", definition.InputMode);
        Assert.Equal("DB1.0", definition.CurrentValueAddress);
    }

    public void Dispose()
    {
        TryDeleteDirectory(_workspace);
        TryDelete(_legacyDbPath);
        TryDelete(_targetDbPath);
    }

    private void CreateLegacyDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_legacyDbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE v_Basic (
    Id TEXT PRIMARY KEY,
    Site TEXT,
    Factory TEXT,
    Area TEXT,
    Procedure TEXT,
    EquipmentNum TEXT,
    ResourceNum TEXT,
    PlcType TEXT,
    PlcIp TEXT,
    Port INTEGER,
    ShutdownPoint TEXT,
    SiemensRack INTEGER,
    SiemensSlot INTEGER,
    IsStringReverse INTEGER
);
CREATE TABLE v_VulnerableParts (
    Id TEXT PRIMARY KEY,
    BasicModelId TEXT,
    Name TEXT,
    Input TEXT,
    CurrentValuePoint TEXT,
    CurrentValueDataType TEXT,
    WarnValuePoint TEXT,
    WarnValueDataType TEXT,
    ShutdownValuePoint TEXT,
    ShutdownValueDataType TEXT,
    IsShutdown INTEGER,
    CodeMinLength INTEGER,
    CodeMaxLength INTEGER,
    LifeType TEXT,
    PlcZeroClear TEXT,
    CodeWritePlcPoint TEXT
);
CREATE TABLE v_ReplaceRecord (
    BasicModelId TEXT,
    Site TEXT,
    Name TEXT,
    OldNo TEXT,
    NewNo TEXT,
    CurrentValue TEXT,
    WarnValue TEXT,
    ShutdownValue TEXT,
    OperatorNo TEXT,
    OperatorUser TEXT,
    ReplaceMessage TEXT,
    DateTime TEXT,
    DataType TEXT,
    DataValue TEXT
);
CREATE TABLE v_exceedlimitinfo (
    BasicId TEXT,
    Name TEXT,
    CurrentValue REAL,
    ShutdownValue REAL,
    DateTime TEXT
);
INSERT INTO v_Basic VALUES ('basic-01', 'S01', 'F01', 'A01', 'P01', 'E01', 'RES-IMPORT-01', 'S7', '127.0.0.1', 102, '!M0.0', 0, 0, 1);
INSERT INTO v_VulnerableParts VALUES ('part-01', 'basic-01', '刀具A', 'Barcode', 'DB1.0', 'Int32', 'DB1.1', 'Int32', 'DB1.2', 'Int32', 1, 8, 32, 'Count', '', 'DB1.4');
INSERT INTO v_ReplaceRecord VALUES ('basic-01', 'S01', '刀具A', 'BARCODE-OLD', 'BARCODE-NEW', '12', '20', '30', 'WORK-01', '张三', '寿命到期', '2025-01-01 08:00:00', 'Int32', '12');
INSERT INTO v_exceedlimitinfo VALUES ('basic-01', '刀具A', 30, 30, '2025-01-02 08:00:00');
""";
        command.ExecuteNonQuery();
    }

    private static void TryDelete(string path)
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
}