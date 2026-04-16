using System.IO;
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

        var count = await verifyContext.BasicConfigurations.CountAsync();
        Assert.Equal(0, count);
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
