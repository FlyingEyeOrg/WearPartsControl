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
        Assert.Contains("SiemensSlot", basicColumns);
        Assert.Contains("IsStringReverse", basicColumns);

        var definitionColumns = await GetColumnsAsync(verifyConnection, "wear_part_definitions");
        Assert.Contains("CurrentValueAddress", definitionColumns);
        Assert.Contains("WarningValueAddress", definitionColumns);
        Assert.Contains("ShutdownValueAddress", definitionColumns);
        Assert.Contains("PlcZeroClearAddress", definitionColumns);
        Assert.Contains("BarcodeWriteAddress", definitionColumns);
        Assert.DoesNotContain("CodeWritePlcPoint", definitionColumns);
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
