using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class ExceedLimitRecordRepository : EfRepositoryBase<WearPartsControlDbContext, ExceedLimitRecordEntity, Guid>, IExceedLimitRecordRepository
{
    public ExceedLimitRecordRepository(WearPartsControlDbContext dbContext)
        : base(dbContext)
    {
    }

    protected override Expression<Func<ExceedLimitRecordEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override async Task<IReadOnlyList<ExceedLimitRecordEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderByDescending(x => x.OccurredAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExceedLimitRecordEntity>> ListByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .Where(x => x.ClientAppConfigurationId == clientAppConfigurationId)
            .OrderByDescending(x => x.OccurredAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsForDayAsync(Guid wearPartDefinitionId, string severity, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        var dayStart = occurredAt.Date;
        var dayEnd = dayStart.AddDays(1);
        var normalizedSeverity = severity.Trim();

        return await Queryable(asNoTracking: true)
            .AnyAsync(x => x.WearPartDefinitionId == wearPartDefinitionId
                && x.Severity == normalizedSeverity
                && x.OccurredAt >= dayStart
                && x.OccurredAt < dayEnd, cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task AddAsync(ExceedLimitRecordEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.AddAsync(entity, cancellationToken);
    }

    public override Task UpdateAsync(ExceedLimitRecordEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.UpdateAsync(entity, cancellationToken);
    }
}