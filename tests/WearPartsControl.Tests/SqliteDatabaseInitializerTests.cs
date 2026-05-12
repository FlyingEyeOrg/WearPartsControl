using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.Tests;

public sealed class SqliteDatabaseInitializerTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public SqliteDatabaseInitializerTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-init-{Guid.NewGuid():N}.db");
        _dbContextFactory = new WearPartsControlDbContextFactory($"Data Source={_dbFilePath}");
    }

    [Fact]
    public async Task InitializeAsync_WhenDatabaseDoesNotExist_ShouldCreateDatabaseAndSchema()
    {
        var initializer = new SqliteDatabaseInitializer(_dbContextFactory);

        await initializer.InitializeAsync();

        Assert.True(File.Exists(_dbFilePath));

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        Assert.True(await verifyContext.Database.CanConnectAsync());

        var count = await verifyContext.ClientAppConfigurations.CountAsync();
        Assert.Equal(0, count);

        await using var verifyConnection = new SqliteConnection($"Data Source={_dbFilePath}");
        await verifyConnection.OpenAsync();

        var definitionColumns = await GetColumnsAsync(verifyConnection, "wear_part_definitions");
        Assert.Contains("ToolChangeId", definitionColumns);

        var toolChangeColumns = await GetColumnsAsync(verifyConnection, "tool_changes");
        Assert.Contains("Name", toolChangeColumns);
        Assert.Contains("Code", toolChangeColumns);
    }

    [Fact]
    public async Task InitializeAsync_WhenDatabaseUsesLegacySchema_ShouldRecreateCurrentSchema()
    {
        await using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
CREATE TABLE basic_configurations (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    FactoryCode TEXT NOT NULL,
    AreaCode TEXT NOT NULL,
    ProcedureCode TEXT NOT NULL,
    EquipmentCode TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PlcProtocolType TEXT NOT NULL,
    PlcIpAddress TEXT NOT NULL,
    PlcPort INTEGER NOT NULL
);
CREATE TABLE wear_part_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PartName TEXT NOT NULL,
    InputMode TEXT NOT NULL,
    CurrentValuePoint TEXT NOT NULL,
    CurrentValueDataType TEXT NOT NULL,
    WarnValuePoint TEXT NOT NULL,
    WarnValueDataType TEXT NOT NULL,
    ShutdownValuePoint TEXT NOT NULL,
    ShutdownValueDataType TEXT NOT NULL,
    IsShutdown INTEGER NOT NULL,
    CodeMinLength INTEGER NOT NULL,
    CodeMaxLength INTEGER NOT NULL,
    LifeType TEXT NOT NULL,
    PlcZeroClear TEXT NOT NULL,
    CodeWritePlcPoint TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_basic_configurations_ResourceNumber ON basic_configurations(ResourceNumber);
""";
            await command.ExecuteNonQueryAsync();
        }

        var initializer = new SqliteDatabaseInitializer(_dbContextFactory);
        await initializer.InitializeAsync();

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var count = await verifyContext.ClientAppConfigurations.CountAsync();
        Assert.Equal(0, count);

        await using var verifyConnection = new SqliteConnection($"Data Source={_dbFilePath}");
        await verifyConnection.OpenAsync();

        var basicColumns = await GetColumnsAsync(verifyConnection, "basic_configurations");
        Assert.Contains("ShutdownPointAddress", basicColumns);
        Assert.Contains("SiemensRack", basicColumns);
        Assert.Contains("SiemensSlot", basicColumns);
        Assert.Contains("IsStringReverse", basicColumns);

        var definitionColumns = await GetColumnsAsync(verifyConnection, "wear_part_definitions");
        Assert.Contains("CurrentValueAddress", definitionColumns);
        Assert.Contains("WarningValueAddress", definitionColumns);
        Assert.Contains("ShutdownValueAddress", definitionColumns);
        Assert.Contains("ToolChangeId", definitionColumns);
        Assert.Contains("PlcZeroClearAddress", definitionColumns);
        Assert.Contains("BarcodeWriteAddress", definitionColumns);
        Assert.DoesNotContain("CodeWritePlcPoint", definitionColumns);

        var toolChangeColumns = await GetColumnsAsync(verifyConnection, "tool_changes");
        Assert.Contains("Name", toolChangeColumns);
        Assert.Contains("Code", toolChangeColumns);
    }

    [Fact]
    public async Task InitializeAsync_WhenOnlyThresholdColumnsAreMissing_ShouldUpgradeWithoutResettingData()
    {
        var configurationId = Guid.NewGuid().ToString();
        var definitionId = Guid.NewGuid().ToString();

        await using (var connection = new SqliteConnection($"Data Source={_dbFilePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $$"""
CREATE TABLE basic_configurations (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    FactoryCode TEXT NOT NULL,
    AreaCode TEXT NOT NULL,
    ProcedureCode TEXT NOT NULL,
    EquipmentCode TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PlcProtocolType TEXT NOT NULL,
    PlcIpAddress TEXT NOT NULL,
    PlcPort INTEGER NOT NULL,
    ShutdownPointAddress TEXT NULL,
    EnableCutterMesValidation INTEGER NOT NULL DEFAULT 0,
    CutterMesWsdl TEXT NULL,
    CutterMesUser TEXT NULL,
    CutterMesPassword TEXT NULL,
    CutterMesSite TEXT NULL,
    SiemensRack INTEGER NOT NULL,
    SiemensSlot INTEGER NOT NULL,
    IsStringReverse INTEGER NOT NULL
);
CREATE UNIQUE INDEX IX_basic_configurations_ResourceNumber ON basic_configurations(ResourceNumber);

CREATE TABLE tool_changes (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    Name TEXT NOT NULL,
    Code TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_tool_changes_Name ON tool_changes(Name);
CREATE UNIQUE INDEX IX_tool_changes_Code ON tool_changes(Code);

CREATE TABLE wear_part_types (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL
);

CREATE TABLE wear_part_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PartName TEXT NOT NULL,
    InputMode TEXT NOT NULL,
    CurrentValueAddress TEXT NOT NULL,
    CurrentValueDataType TEXT NOT NULL,
    WarningValueAddress TEXT NOT NULL,
    WarningValueDataType TEXT NOT NULL,
    ShutdownValueAddress TEXT NOT NULL,
    ShutdownValueDataType TEXT NOT NULL,
    IsShutdown INTEGER NOT NULL,
    CodeMinLength INTEGER NOT NULL,
    CodeMaxLength INTEGER NOT NULL,
    LifetimeType TEXT NOT NULL,
    WearPartTypeId TEXT NULL,
    ToolChangeId TEXT NULL,
    PlcZeroClearAddress TEXT NULL,
    BarcodeWriteAddress TEXT NOT NULL,
    CONSTRAINT FK_wear_part_definitions_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_wear_part_definitions_tool_changes_ToolChangeId
        FOREIGN KEY (ToolChangeId)
        REFERENCES tool_changes (Id)
        ON DELETE SET NULL
);
CREATE UNIQUE INDEX IX_wear_part_definitions_ClientAppConfigurationId_PartName ON wear_part_definitions(ClientAppConfigurationId, PartName);

CREATE TABLE wear_part_replacement_records (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL,
    DeletedAt TEXT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    WearPartDefinitionId TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    PartName TEXT NOT NULL,
    OldBarcode TEXT NULL,
    NewBarcode TEXT NOT NULL,
    CurrentValue TEXT NOT NULL,
    WarningValue TEXT NOT NULL,
    ShutdownValue TEXT NOT NULL,
    OperatorWorkNumber TEXT NOT NULL,
    OperatorUserName TEXT NOT NULL,
    ReplacementReason TEXT NOT NULL,
    ReplacementMessage TEXT NOT NULL,
    ReplacedAt TEXT NOT NULL,
    DataType TEXT NULL,
    DataValue TEXT NULL,
    CONSTRAINT FK_wear_part_replacement_records_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_wear_part_replacement_records_wear_part_definitions_WearPartDefinitionId
        FOREIGN KEY (WearPartDefinitionId)
        REFERENCES wear_part_definitions (Id)
        ON DELETE CASCADE
);
CREATE INDEX IX_wear_part_replacement_records_WearPartDefinitionId_ReplacedAt ON wear_part_replacement_records(WearPartDefinitionId, ReplacedAt DESC);
CREATE INDEX IX_wear_part_replacement_records_WearPartDefinitionId_NewBarcode ON wear_part_replacement_records(WearPartDefinitionId, NewBarcode);

CREATE TABLE exceed_limit_records (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    WearPartDefinitionId TEXT NOT NULL,
    PartName TEXT NOT NULL,
    CurrentValue REAL NOT NULL,
    WarningValue REAL NOT NULL,
    ShutdownValue REAL NOT NULL,
    Severity TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    NotificationMessage TEXT NOT NULL,
    CONSTRAINT FK_exceed_limit_records_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_exceed_limit_records_wear_part_definitions_WearPartDefinitionId
        FOREIGN KEY (WearPartDefinitionId)
        REFERENCES wear_part_definitions (Id)
        ON DELETE CASCADE
);
CREATE INDEX IX_exceed_limit_records_WearPartDefinitionId_Severity_OccurredAt ON exceed_limit_records(WearPartDefinitionId, Severity, OccurredAt DESC);

INSERT INTO basic_configurations (
    Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, SiteCode, FactoryCode, AreaCode, ProcedureCode, EquipmentCode,
    ResourceNumber, PlcProtocolType, PlcIpAddress, PlcPort, ShutdownPointAddress, EnableCutterMesValidation,
    CutterMesWsdl, CutterMesUser, CutterMesPassword, CutterMesSite, SiemensRack, SiemensSlot, IsStringReverse)
VALUES (
    '{{configurationId}}', '2026-05-12T00:00:00Z', '2026-05-12T00:00:00Z', 'tester', 'tester', 'S01', 'F01', 'A01', 'P01', 'E01',
    'RES-TH-01', 'S7', '127.0.0.1', 102, 'M0.0', 0,
    NULL, NULL, NULL, NULL, 0, 1, 0);

INSERT INTO wear_part_definitions (
    Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, ClientAppConfigurationId, ResourceNumber, PartName, InputMode,
    CurrentValueAddress, CurrentValueDataType, WarningValueAddress, WarningValueDataType, ShutdownValueAddress, ShutdownValueDataType,
    IsShutdown, CodeMinLength, CodeMaxLength, LifetimeType, WearPartTypeId, ToolChangeId, PlcZeroClearAddress, BarcodeWriteAddress)
VALUES (
    '{{definitionId}}', '2026-05-12T00:00:00Z', '2026-05-12T00:00:00Z', 'tester', 'tester', '{{configurationId}}', 'RES-TH-01', '刀具A', 'Barcode',
    'DB1.0', 'Int32', 'DB1.1', 'Int32', 'DB1.2', 'Int32',
    1, 8, 32, 'Count', NULL, NULL, 'DB1.3', 'DB1.4');
""";
            await command.ExecuteNonQueryAsync();
        }

        var initializer = new SqliteDatabaseInitializer(_dbContextFactory);
        await initializer.InitializeAsync();

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await verifyContext.ClientAppConfigurations.CountAsync());
        Assert.Equal(1, await verifyContext.WearPartDefinitions.CountAsync());

        var definition = await verifyContext.WearPartDefinitions.SingleAsync();
        Assert.Equal(0d, definition.WarningLifetimeThreshold);
        Assert.Equal(0d, definition.ShutdownLifetimeThreshold);

        await using var verifyConnection = new SqliteConnection($"Data Source={_dbFilePath}");
        await verifyConnection.OpenAsync();

        var definitionColumns = await GetColumnsAsync(verifyConnection, "wear_part_definitions");
        Assert.Contains("WarningLifetimeThreshold", definitionColumns);
        Assert.Contains("ShutdownLifetimeThreshold", definitionColumns);
    }

    private static async Task<HashSet<string>> GetColumnsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
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
}
