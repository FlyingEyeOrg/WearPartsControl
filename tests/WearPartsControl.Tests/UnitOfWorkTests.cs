using System.IO;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class UnitOfWorkTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public UnitOfWorkTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-uow-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbFilePath}";
        _dbContextFactory = new WearPartsControlDbContextFactory(connectionString);

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldNotPersistChanges()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await using IUnitOfWork<DbContextBase> unitOfWork = new EfUnitOfWork<DbContextBase>(dbContext);

        await unitOfWork.BeginTransactionAsync();

        await dbContext.BasicConfigurations.AddAsync(new BasicConfigurationEntity
        {
            Id = Guid.NewGuid(),
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "E01",
            ResourceNumber = "R-UOW-01",
            PlcProtocolType = "S7",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            UpdatedAt = DateTime.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
        await unitOfWork.RollbackTransactionAsync();

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var exists = await verifyContext.BasicConfigurations.AnyAsync(x => x.ResourceNumber == "R-UOW-01");

        Assert.False(exists);
    }

    [Fact]
    public async Task CommitTransactionAsync_ShouldPersistChanges()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await using IUnitOfWork<DbContextBase> unitOfWork = new EfUnitOfWork<DbContextBase>(dbContext);

        await unitOfWork.BeginTransactionAsync();

        await dbContext.BasicConfigurations.AddAsync(new BasicConfigurationEntity
        {
            Id = Guid.NewGuid(),
            SiteCode = "S02",
            FactoryCode = "F02",
            AreaCode = "A02",
            ProcedureCode = "P02",
            EquipmentCode = "E02",
            ResourceNumber = "R-UOW-02",
            PlcProtocolType = "S7",
            PlcIpAddress = "127.0.0.2",
            PlcPort = 103,
            UpdatedAt = DateTime.UtcNow
        });

        await unitOfWork.CommitTransactionAsync();

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var exists = await verifyContext.BasicConfigurations.AnyAsync(x => x.ResourceNumber == "R-UOW-02");

        Assert.True(exists);
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
