using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.Database;

public sealed class PartDbContextFactory : IDesignTimeDbContextFactory<PartDbContext>, IDbContextFactory<PartDbContext>
{
    private readonly string _connectionString;

    public PartDbContextFactory()
    {
        _connectionString = BuildDefaultConnectionString();
    }

    public PartDbContextFactory(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? BuildDefaultConnectionString()
            : connectionString;
    }

    public PartDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PartDbContext>();
        optionsBuilder.UseSqlite(_connectionString);
        return new PartDbContext(optionsBuilder.Options);
    }

    PartDbContext IDesignTimeDbContextFactory<PartDbContext>.CreateDbContext(string[] args)
    {
        return CreateDbContext();
    }

    public Task<PartDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }

    private static string BuildDefaultConnectionString()
    {
        var dbDirectory = Path.Combine(AppContext.BaseDirectory, "saveinfo", "db");
        Directory.CreateDirectory(dbDirectory);
        var dbPath = Path.Combine(dbDirectory, "wearparts.db");
        return $"Data Source={dbPath}";
    }
}
