using System.IO;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.Tests;

public sealed class WearPartsControlDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithDefaultConstructor_ShouldUsePrivateDataDatabasePath()
    {
        var expectedPath = Path.Combine(PortableDataPaths.DatabaseDirectory, "wear-parts-control.db");

        using var dbContext = new WearPartsControlDbContextFactory().CreateDbContext();
        dbContext.Database.EnsureCreated();

        var connectionString = dbContext.Database.GetConnectionString();

        Assert.Equal($"Data Source={expectedPath}", connectionString);
        Assert.True(File.Exists(expectedPath));

        dbContext.Database.EnsureDeleted();
    }

    [Fact]
    public async Task CreateDbContextAsync_WithCustomConnectionString_ShouldConnectToSpecifiedDatabase()
    {
        var dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-factory-{Guid.NewGuid():N}.db");
        var factory = new WearPartsControlDbContextFactory($"Data Source={dbFilePath}");

        await using var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.True(File.Exists(dbFilePath));

        await dbContext.Database.EnsureDeletedAsync();
    }
}
