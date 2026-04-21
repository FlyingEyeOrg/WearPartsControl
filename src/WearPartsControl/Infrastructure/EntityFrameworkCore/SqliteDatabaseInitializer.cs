using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedTables = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["basic_configurations"] =
        [
            "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "SiteCode", "FactoryCode", "AreaCode", "ProcedureCode",
            "EquipmentCode", "ResourceNumber", "PlcProtocolType", "PlcIpAddress", "PlcPort", "ShutdownPointAddress", "SiemensRack", "SiemensSlot", "IsStringReverse"
        ],
        ["wear_part_definitions"] =
        [
            "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "ClientAppConfigurationId", "ResourceNumber", "PartName", "InputMode",
            "CurrentValueAddress", "CurrentValueDataType", "WarningValueAddress", "WarningValueDataType", "ShutdownValueAddress", "ShutdownValueDataType",
            "IsShutdown", "CodeMinLength", "CodeMaxLength", "LifetimeType", "PlcZeroClearAddress", "BarcodeWriteAddress"
        ],
        ["wear_part_replacement_records"] =
        [
            "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt", "ClientAppConfigurationId", "WearPartDefinitionId",
            "SiteCode", "PartName", "OldBarcode", "NewBarcode", "CurrentValue", "WarningValue", "ShutdownValue", "OperatorWorkNumber",
            "OperatorUserName", "ReplacementReason", "ReplacementMessage", "ReplacedAt", "DataType", "DataValue"
        ],
        ["exceed_limit_records"] =
        [
            "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "ClientAppConfigurationId", "WearPartDefinitionId", "PartName",
            "CurrentValue", "WarningValue", "ShutdownValue", "Severity", "OccurredAt", "NotificationMessage"
        ]
    };

    private readonly IDbContextFactory<WearPartsControlDbContext> _dbContextFactory;

    public SqliteDatabaseInitializer(IDbContextFactory<WearPartsControlDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (await RequiresDatabaseResetAsync(cancellationToken).ConfigureAwait(false))
        {
            await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var migrations = dbContext.Database.GetMigrations();
        if (migrations.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> RequiresDatabaseResetAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var existingTables = await GetUserTableNamesAsync(connection, cancellationToken).ConfigureAwait(false);
            if (existingTables.Count == 0)
            {
                return false;
            }

            var expectedTableNames = ExpectedTables.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!existingTables.SetEquals(expectedTableNames))
            {
                return true;
            }

            foreach (var table in ExpectedTables)
            {
                var actualColumns = await GetColumnsAsync(connection, table.Key, cancellationToken).ConfigureAwait(false);
                if (!actualColumns.SetEquals(table.Value))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> GetUserTableNamesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
  AND name <> '__EFMigrationsHistory';
""";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
