using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Domain.Services;
using WearPartsControl.Infrastructure;
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
        var repository = new WearPartRepository(writeContext, new WearPartDefinitionDomainService());
        IUnitOfWork unitOfWork = writeContext;

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
