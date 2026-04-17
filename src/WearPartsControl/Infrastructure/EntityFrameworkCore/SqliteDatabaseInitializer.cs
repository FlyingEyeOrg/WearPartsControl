using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<WearPartsControlDbContext> _dbContextFactory;

    public SqliteDatabaseInitializer(IDbContextFactory<WearPartsControlDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
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

        await EnsureClientAppConfigurationSchemaAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureClientAppConfigurationSchemaAsync(WearPartsControlDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (!await TableExistsAsync(connection, "basic_configurations", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await EnsureColumnExistsAsync(connection, "basic_configurations", "ShutdownPointAddress", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            await EnsureColumnExistsAsync(connection, "basic_configurations", "SiemensSlot", "INTEGER NOT NULL DEFAULT 1", cancellationToken).ConfigureAwait(false);
            await EnsureColumnExistsAsync(connection, "basic_configurations", "IsStringReverse", "INTEGER NOT NULL DEFAULT 1", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task EnsureColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
