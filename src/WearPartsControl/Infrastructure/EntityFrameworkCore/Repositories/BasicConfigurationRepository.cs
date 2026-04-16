using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using System.Linq.Expressions;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class BasicConfigurationRepository : EfRepositoryBase<DbContextBase, BasicConfigurationEntity, Guid>, IBasicConfigurationRepository
{
    public BasicConfigurationRepository(DbContextBase dbContext, IUnitOfWork<DbContextBase> unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    protected override Expression<Func<BasicConfigurationEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override Task<BasicConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Queryable(asNoTracking: true)
            .Include(x => x.WearPartDefinitions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public override async Task<IReadOnlyList<BasicConfigurationEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderBy(x => x.ResourceNumber)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BasicConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var normalized = resourceNumber.Trim();

        return await Queryable(asNoTracking: true)
            .Include(x => x.WearPartDefinitions)
            .FirstOrDefaultAsync(x => x.ResourceNumber == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = resourceNumber.Trim();

        return await Queryable(asNoTracking: true)
            .AnyAsync(x => x.ResourceNumber == normalized && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task AddAsync(BasicConfigurationEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.AddAsync(entity, cancellationToken);
    }

    public override Task UpdateAsync(BasicConfigurationEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.UpdateAsync(entity, cancellationToken);
    }
}
