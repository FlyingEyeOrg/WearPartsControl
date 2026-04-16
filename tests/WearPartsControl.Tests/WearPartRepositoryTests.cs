using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Domain.Services;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;
using System.IO;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartRepositoryTests : IDisposable
{
    private readonly string _dbFilePath;
    private readonly WearPartsControlDbContextFactory _dbContextFactory;

    public WearPartRepositoryTests()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"wearparts-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbFilePath}";
        _dbContextFactory = new WearPartsControlDbContextFactory(connectionString);

        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ThenListByBasicConfigurationAsync_ShouldPersistEntity()
    {
        var basicConfigurationId = Guid.NewGuid();

        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync())
        {
            dbContext.BasicConfigurations.Add(new BasicConfigurationEntity
            {
                Id = basicConfigurationId,
                SiteCode = "S01",
                FactoryCode = "F01",
                AreaCode = "A01",
                ProcedureCode = "P01",
                EquipmentCode = "E01",
                ResourceNumber = "R001",
                PlcProtocolType = "S7",
                PlcIpAddress = "192.168.1.1",
                PlcPort = 102,
                UpdatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        await using var writeContext = await _dbContextFactory.CreateDbContextAsync();
        await using IUnitOfWork<WearPartsControlDbContext> unitOfWork = new EfUnitOfWork<WearPartsControlDbContext>(writeContext);
        var repository = new WearPartRepository(writeContext, new WearPartDefinitionDomainService());

        var definition = new WearPartDefinitionEntity
        {
            Id = Guid.NewGuid(),
            BasicConfigurationId = basicConfigurationId,
            ResourceNumber = "R001",
            PartName = "Knife-01",
            CurrentValueAddress = "DB1.0",
            WarningValueAddress = "DB1.1",
            ShutdownValueAddress = "DB1.2",
            CreatedBy = "manual-user",
            UpdatedBy = "manual-user",
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(definition);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = await _dbContextFactory.CreateDbContextAsync();
        var readRepository = new WearPartRepository(readContext, new WearPartDefinitionDomainService());
        var result = await readRepository.ListByBasicConfigurationAsync(basicConfigurationId);

        Assert.Single(result);
        Assert.Equal("Knife-01", result[0].PartName);
        Assert.Equal("manual-user", result[0].CreatedBy);
        Assert.Equal("manual-user", result[0].UpdatedBy);
    }

    [Fact]
    public async Task AddAsync_WhenRepositoryCreatedManually_ShouldUseFallbackUnitOfWorkAndAssignDefaults()
    {
        var basicConfigurationId = await SeedBasicConfigurationAsync("R002");

        await using var writeContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new WearPartRepository(writeContext, new WearPartDefinitionDomainService());

        var definition = new WearPartDefinitionEntity
        {
            Id = Guid.Empty,
            BasicConfigurationId = basicConfigurationId,
            ResourceNumber = "R002",
            PartName = "Knife-02",
            CurrentValueAddress = "DB2.0",
            WarningValueAddress = "DB2.1",
            ShutdownValueAddress = "DB2.2",
            CreatedAt = null,
            UpdatedAt = null,
            CreatedBy = string.Empty,
            UpdatedBy = string.Empty
        };

        await repository.AddAsync(definition);
        await repository.UnitOfWork.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, definition.Id);
        Assert.IsType<EfUnitOfWork<WearPartsControlDbContext>>(repository.UnitOfWork);
        Assert.NotNull(definition.CreatedAt);
        Assert.NotNull(definition.UpdatedAt);
        Assert.Equal(string.Empty, definition.CreatedBy);
        Assert.Equal(string.Empty, definition.UpdatedBy);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldMarkDeleted_AndExcludeEntityFromRepositoryQueries()
    {
        var basicConfigurationId = await SeedBasicConfigurationAsync("R003");

        await using var writeContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new WearPartRepository(writeContext, new WearPartDefinitionDomainService());

        var definition = new WearPartDefinitionEntity
        {
            Id = Guid.NewGuid(),
            BasicConfigurationId = basicConfigurationId,
            ResourceNumber = "R003",
            PartName = "Knife-03",
            CurrentValueAddress = "DB3.0",
            WarningValueAddress = "DB3.1",
            ShutdownValueAddress = "DB3.2",
            CreatedBy = "manual-user",
            UpdatedBy = "manual-user"
        };

        await repository.AddAsync(definition);
        await repository.UnitOfWork.SaveChangesAsync();

        await repository.SoftDeleteAsync(definition.Id);
        await repository.UnitOfWork.SaveChangesAsync();

        await using var readContext = await _dbContextFactory.CreateDbContextAsync();
        var readRepository = new WearPartRepository(readContext, new WearPartDefinitionDomainService());
        var result = await readRepository.ListByBasicConfigurationAsync(basicConfigurationId);

        Assert.Empty(result);

        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var deleted = await verifyContext.WearPartDefinitions
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == definition.Id);

        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }

    private async Task<Guid> SeedBasicConfigurationAsync(string resourceNumber)
    {
        var basicConfigurationId = Guid.NewGuid();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        dbContext.BasicConfigurations.Add(new BasicConfigurationEntity
        {
            Id = basicConfigurationId,
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "E01",
            ResourceNumber = resourceNumber,
            PlcProtocolType = "S7",
            PlcIpAddress = "192.168.1.1",
            PlcPort = 102,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
        return basicConfigurationId;
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
            // SQLite 连接释放存在轻微延迟，文件清理失败不影响测试正确性。
        }
    }
}
