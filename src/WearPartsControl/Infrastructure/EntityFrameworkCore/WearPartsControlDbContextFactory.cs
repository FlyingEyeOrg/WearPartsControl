using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class WearPartsControlDbContextFactory : IDesignTimeDbContextFactory<WearPartsControlDbContext>, IDbContextFactory<WearPartsControlDbContext>
{
    private readonly string _connectionString;

    public WearPartsControlDbContextFactory()
    {
        _connectionString = BuildDefaultConnectionString();
    }

    public WearPartsControlDbContextFactory(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? BuildDefaultConnectionString()
            : connectionString;
    }

    public WearPartsControlDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<WearPartsControlDbContext>();
        optionsBuilder.UseSqlite(_connectionString);
        return new WearPartsControlDbContext(optionsBuilder.Options);
    }

    WearPartsControlDbContext IDesignTimeDbContextFactory<WearPartsControlDbContext>.CreateDbContext(string[] args)
    {
        return CreateDbContext();
    }

    public Task<WearPartsControlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }

    private static string BuildDefaultConnectionString()
    {
        var dbDirectory = PortableDataPaths.DatabaseDirectory;
        Directory.CreateDirectory(dbDirectory);
        var dbPath = Path.Combine(dbDirectory, "wear-parts-control.db");
        return $"Data Source={dbPath}";
    }
}
