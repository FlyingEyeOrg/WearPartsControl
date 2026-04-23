using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class WearPartReplacementRecordRepository : EfRepositoryBase<WearPartsControlDbContext, WearPartReplacementRecordEntity, Guid>, IWearPartReplacementRecordRepository
{
    public WearPartReplacementRecordRepository(WearPartsControlDbContext dbContext)
        : base(dbContext)
    {
    }

    protected override Expression<Func<WearPartReplacementRecordEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override async Task<IReadOnlyList<WearPartReplacementRecordEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderByDescending(x => x.ReplacedAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WearPartReplacementRecordEntity>> ListByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .Where(x => x.ClientAppConfigurationId == clientAppConfigurationId)
            .OrderByDescending(x => x.ReplacedAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WearPartReplacementRecordEntity?> GetLatestByDefinitionAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .Where(x => x.WearPartDefinitionId == wearPartDefinitionId)
            .OrderByDescending(x => x.ReplacedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WearPartReplacementRecordEntity?> GetLatestByCurrentBarcodeAsync(Guid wearPartDefinitionId, string currentBarcode, CancellationToken cancellationToken = default)
    {
        var normalizedBarcode = currentBarcode.Trim();

        return await Queryable(asNoTracking: true)
            .Where(x => x.WearPartDefinitionId == wearPartDefinitionId && x.CurrentBarcode == normalizedBarcode)
            .OrderByDescending(x => x.ReplacedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsNewBarcodeAsync(Guid wearPartDefinitionId, string newBarcode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedBarcode = newBarcode.Trim();

        return await Queryable(asNoTracking: true)
            .AnyAsync(x => x.WearPartDefinitionId == wearPartDefinitionId
                && x.NewBarcode == normalizedBarcode
                && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task AddAsync(WearPartReplacementRecordEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.AddAsync(entity, cancellationToken);
    }

    public override Task UpdateAsync(WearPartReplacementRecordEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.UpdateAsync(entity, cancellationToken);
    }
}