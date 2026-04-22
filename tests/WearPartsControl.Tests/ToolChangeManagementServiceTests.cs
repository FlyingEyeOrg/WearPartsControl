using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ToolChangeManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldPersistToolChangeAndSaveChanges()
    {
        var repository = new FakeToolChangeRepository();
        var service = new ToolChangeManagementService(CreateLoggedInAccessor(), repository);

        var created = await service.CreateAsync(new ToolChangeDefinition { Name = "标准刀", Code = "TL-01" });

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("标准刀", created.Name);
        Assert.Equal("TL-01", repository.Entities[0].Code);
        Assert.Equal(1, repository.UnitOfWorkImpl.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WhenCodeDuplicated_ShouldThrowUserFriendlyException()
    {
        var repository = new FakeToolChangeRepository(new ToolChangeEntity { Id = Guid.NewGuid(), Name = "标准刀", Code = "TL-01" });
        var service = new ToolChangeManagementService(CreateLoggedInAccessor(), repository);

        await Assert.ThrowsAsync<UserFriendlyException>(() => service.CreateAsync(new ToolChangeDefinition { Name = "备用刀", Code = "TL-01" }));
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingEntity()
    {
        var entity = new ToolChangeEntity { Id = Guid.NewGuid(), Name = "标准刀", Code = "TL-01" };
        var repository = new FakeToolChangeRepository(entity);
        var service = new ToolChangeManagementService(CreateLoggedInAccessor(), repository);

        var updated = await service.UpdateAsync(new ToolChangeDefinition { Id = entity.Id, Name = "标准刀-改", Code = "TL-02" });

        Assert.Equal("标准刀-改", updated.Name);
        Assert.Equal("TL-02", repository.Entities[0].Code);
    }

    private static CurrentUserAccessor CreateLoggedInAccessor()
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 1
        });
        return accessor;
    }

    private sealed class FakeToolChangeRepository : IToolChangeRepository
    {
        public FakeToolChangeRepository(params ToolChangeEntity[] entities)
        {
            Entities = entities.ToList();
        }

        public List<ToolChangeEntity> Entities { get; }

        public FakeUnitOfWork UnitOfWorkImpl { get; } = new();

        public IUnitOfWork UnitOfWork => UnitOfWorkImpl;

        public Task<ToolChangeEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entities.FirstOrDefault(x => x.Id == id));
        }

        public Task<IReadOnlyList<ToolChangeEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolChangeEntity>>(Entities.OrderByDescending(x => x.CreatedAt).ToArray());
        }

        public Task AddAsync(ToolChangeEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            Entities.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ToolChangeEntity entity, CancellationToken cancellationToken = default)
        {
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

        public Task<bool> ExistsNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entities.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || x.Id != excludeId.Value)));
        }

        public Task<bool> ExistsCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entities.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || x.Id != excludeId.Value)));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}