using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartManagementServiceTests
{
    [Fact]
    public async Task CreateDefinitionAsync_WhenUserNotLoggedIn_ShouldThrowAuthorizationException()
    {
        var currentUserAccessor = new CurrentUserAccessor();
        var basicRepository = new FakeClientAppConfigurationRepository();
        var wearPartRepository = new FakeWearPartRepository();
        var service = new WearPartManagementService(currentUserAccessor, basicRepository, wearPartRepository);

        await Assert.ThrowsAsync<AuthorizationException>(() => service.CreateDefinitionAsync(CreateDefinitionModel()));
    }

    [Fact]
    public async Task CreateDefinitionAsync_ShouldPersistDefinitionAndSaveChanges()
    {
        var currentUserAccessor = CreateLoggedInAccessor();
        var basicConfiguration = CreateClientAppConfiguration("R100");
        var basicRepository = new FakeClientAppConfigurationRepository(basicConfiguration);
        var wearPartRepository = new FakeWearPartRepository();
        var service = new WearPartManagementService(currentUserAccessor, basicRepository, wearPartRepository);

        var created = await service.CreateDefinitionAsync(CreateDefinitionModel(basicConfiguration.Id, basicConfiguration.ResourceNumber));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("R100", created.ResourceNumber);
        Assert.Single(wearPartRepository.Entities);
        Assert.Equal("刀具A", wearPartRepository.Entities[0].PartName);
        Assert.Equal(1, wearPartRepository.UnitOfWorkImpl.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateDefinitionAsync_WhenPartNameDuplicated_ShouldThrowUserFriendlyException()
    {
        var currentUserAccessor = CreateLoggedInAccessor();
        var basicConfiguration = CreateClientAppConfiguration("R200");
        var basicRepository = new FakeClientAppConfigurationRepository(basicConfiguration);
        var existing = CreateEntity(basicConfiguration, "刀具A");
        var duplicate = CreateEntity(basicConfiguration, "刀具B");
        var wearPartRepository = new FakeWearPartRepository(existing, duplicate);
        var service = new WearPartManagementService(currentUserAccessor, basicRepository, wearPartRepository);

        var model = CreateDefinitionModel(basicConfiguration.Id, basicConfiguration.ResourceNumber);
        model.Id = existing.Id;
        model.PartName = duplicate.PartName;

        await Assert.ThrowsAsync<UserFriendlyException>(() => service.UpdateDefinitionAsync(model));
    }

    [Fact]
    public async Task CopyDefinitionsAsync_ShouldCloneDefinitionsToTargetResource()
    {
        var currentUserAccessor = CreateLoggedInAccessor();
        var source = CreateClientAppConfiguration("R300");
        var target = CreateClientAppConfiguration("R301");
        var basicRepository = new FakeClientAppConfigurationRepository(source, target);
        var sourceDefinition = CreateEntity(source, "刀具A");
        var wearPartRepository = new FakeWearPartRepository(sourceDefinition);
        var service = new WearPartManagementService(currentUserAccessor, basicRepository, wearPartRepository);

        var copiedCount = await service.CopyDefinitionsAsync(source.ResourceNumber, target.ResourceNumber);

        Assert.Equal(1, copiedCount);
        Assert.Equal(2, wearPartRepository.Entities.Count);
        var copied = wearPartRepository.Entities.Single(x => x.ClientAppConfigurationId == target.Id);
        Assert.NotEqual(sourceDefinition.Id, copied.Id);
        Assert.Equal(target.ResourceNumber, copied.ResourceNumber);
        Assert.Equal(sourceDefinition.PartName, copied.PartName);
        Assert.Equal(1, wearPartRepository.UnitOfWorkImpl.SaveChangesCallCount);
    }

    private static CurrentUserAccessor CreateLoggedInAccessor()
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new WearPartsControl.ApplicationServices.LoginService.MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 1
        });
        return accessor;
    }

    private static ClientAppConfigurationEntity CreateClientAppConfiguration(string resourceNumber)
    {
        return new ClientAppConfigurationEntity
        {
            Id = Guid.NewGuid(),
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "E01",
            ResourceNumber = resourceNumber,
            PlcProtocolType = "S7",
            PlcIpAddress = "127.0.0.1",
            PlcPort = 102,
            ShutdownPointAddress = "DB1.200",
            SiemensSlot = 1,
            IsStringReverse = true
        };
    }

    private static WearPartDefinition CreateDefinitionModel(Guid? clientAppConfigurationId = null, string resourceNumber = "R100")
    {
        return new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = clientAppConfigurationId ?? Guid.Empty,
            ResourceNumber = resourceNumber,
            PartName = "刀具A",
            InputMode = "Barcode",
            CurrentValueAddress = "DB1.0",
            CurrentValueDataType = "Int32",
            WarningValueAddress = "DB1.1",
            WarningValueDataType = "Int32",
            ShutdownValueAddress = "DB1.2",
            ShutdownValueDataType = "Int32",
            IsShutdown = true,
            CodeMinLength = 10,
            CodeMaxLength = 30,
            LifetimeType = "Count",
            PlcZeroClearAddress = "DB1.3",
            BarcodeWriteAddress = "DB1.4"
        };
    }

    private static WearPartDefinitionEntity CreateEntity(ClientAppConfigurationEntity basicConfiguration, string partName)
    {
        return new WearPartDefinitionEntity
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = basicConfiguration.Id,
            ResourceNumber = basicConfiguration.ResourceNumber,
            PartName = partName,
            InputMode = "Barcode",
            CurrentValueAddress = "DB1.0",
            CurrentValueDataType = "Int32",
            WarningValueAddress = "DB1.1",
            WarningValueDataType = "Int32",
            ShutdownValueAddress = "DB1.2",
            ShutdownValueDataType = "Int32",
            IsShutdown = true,
            CodeMinLength = 10,
            CodeMaxLength = 30,
            LifetimeType = "Count",
            PlcZeroClearAddress = "DB1.3",
            BarcodeWriteAddress = "DB1.4"
        };
    }

    private sealed class FakeClientAppConfigurationRepository : IClientAppConfigurationRepository
    {
        private readonly List<ClientAppConfigurationEntity> _entities;

        public FakeClientAppConfigurationRepository(params ClientAppConfigurationEntity[] entities)
        {
            _entities = entities.ToList();
        }

        public IUnitOfWork UnitOfWork { get; } = new FakeUnitOfWork();

        public Task<ClientAppConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entities.FirstOrDefault(x => x.Id == id));
        }

        public Task<IReadOnlyList<ClientAppConfigurationEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ClientAppConfigurationEntity>>(_entities.ToArray());
        }

        public Task AddAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            _entities.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _entities.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(id, cancellationToken);
        }

        public Task<ClientAppConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entities.FirstOrDefault(x => x.ResourceNumber == resourceNumber));
        }

        public Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entities.Any(x => x.ResourceNumber == resourceNumber && (!excludeId.HasValue || x.Id != excludeId.Value)));
        }
    }

    private sealed class FakeWearPartRepository : IWearPartRepository
    {
        public FakeWearPartRepository(params WearPartDefinitionEntity[] entities)
        {
            Entities = entities.ToList();
        }

        public List<WearPartDefinitionEntity> Entities { get; }

        public FakeUnitOfWork UnitOfWorkImpl { get; } = new();

        public IUnitOfWork UnitOfWork => UnitOfWorkImpl;

        public Task<WearPartDefinitionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entities.FirstOrDefault(x => x.Id == id));
        }

        public Task<IReadOnlyList<WearPartDefinitionEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartDefinitionEntity>>(Entities.OrderBy(x => x.PartName).ToArray());
        }

        public Task AddAsync(WearPartDefinitionEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            Entities.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WearPartDefinitionEntity entity, CancellationToken cancellationToken = default)
        {
            var index = Entities.FindIndex(x => x.Id == entity.Id);
            if (index >= 0)
            {
                Entities[index] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Entities.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<WearPartDefinitionEntity>> ListByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartDefinitionEntity>>(Entities.Where(x => x.ClientAppConfigurationId == clientAppConfigurationId).OrderBy(x => x.PartName).ToArray());
        }

        public Task<bool> ExistsPartNameAsync(Guid clientAppConfigurationId, string partName, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Entities.Any(x =>
                    x.ClientAppConfigurationId == clientAppConfigurationId
                    && string.Equals(x.PartName, partName, StringComparison.OrdinalIgnoreCase)
                    && (!excludeId.HasValue || x.Id != excludeId.Value)));
        }

        public Task AddRangeAsync(IReadOnlyCollection<WearPartDefinitionEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.NewGuid();
                }

                Entities.Add(entity);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}