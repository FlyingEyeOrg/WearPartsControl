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
    public async Task InitializeAsync_WhenDatabaseIsMissingIsStringReverseColumn_ShouldPatchSchema()
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
    PlcPort INTEGER NOT NULL,
    ShutdownPointAddress TEXT NULL,
    SiemensSlot INTEGER NOT NULL
);
CREATE UNIQUE INDEX IX_basic_configurations_ResourceNumber ON basic_configurations(ResourceNumber);
""";
            await command.ExecuteNonQueryAsync();
        }

        var initializer = new SqliteDatabaseInitializer(_dbContextFactory);
        await initializer.InitializeAsync();

        await using var verifyConnection = new SqliteConnection($"Data Source={_dbFilePath}");
        await verifyConnection.OpenAsync();
        await using var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "PRAGMA table_info(basic_configurations);";
        await using var reader = await verifyCommand.ExecuteReaderAsync();

        var hasIsStringReverse = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), "IsStringReverse", StringComparison.OrdinalIgnoreCase))
            {
                hasIsStringReverse = true;
                break;
            }
        }

        Assert.True(hasIsStringReverse);
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
