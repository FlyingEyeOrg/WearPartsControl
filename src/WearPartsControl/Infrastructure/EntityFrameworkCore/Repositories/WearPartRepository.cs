using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Domain.Services;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using System.Linq.Expressions;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class WearPartRepository : EfRepositoryBase<DbContextBase, WearPartDefinitionEntity, Guid>, IWearPartRepository
{
    private readonly WearPartDefinitionDomainService _domainService;

    public WearPartRepository(
        DbContextBase dbContext,
        ICurrentUser currentUser,
        WearPartDefinitionDomainService domainService)
        : base(dbContext, currentUser)
    {
        _domainService = domainService;
    }

    protected override Expression<Func<WearPartDefinitionEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override async Task<IReadOnlyList<WearPartDefinitionEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderBy(x => x.PartName)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WearPartDefinitionEntity>> ListByBasicConfigurationAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .Where(x => x.BasicConfigurationId == basicConfigurationId)
            .OrderBy(x => x.PartName)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsPartNameAsync(Guid basicConfigurationId, string partName, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedPartName = partName.Trim();

        return await Queryable(asNoTracking: true)
            .AnyAsync(x =>
                x.BasicConfigurationId == basicConfigurationId
                && x.PartName == normalizedPartName
                && (!excludeId.HasValue || x.Id != excludeId.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task AddAsync(WearPartDefinitionEntity entity, CancellationToken cancellationToken = default)
    {
        _domainService.ValidateEntity(entity);
        return base.AddAsync(entity, cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<WearPartDefinitionEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            _domainService.ValidateEntity(entity);
        }

        _domainService.ValidateUniquePartNames(entities);

        await Set.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    public override Task UpdateAsync(WearPartDefinitionEntity entity, CancellationToken cancellationToken = default)
    {
        _domainService.ValidateEntity(entity);
        return base.UpdateAsync(entity, cancellationToken);
    }
}
