using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class ToolChangeRepository : EfRepositoryBase<WearPartsControlDbContext, ToolChangeEntity, Guid>, IToolChangeRepository
{
    public ToolChangeRepository(WearPartsControlDbContext dbContext)
        : base(dbContext)
    {
    }

    protected override Expression<Func<ToolChangeEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override async Task<IReadOnlyList<ToolChangeEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        return await Queryable(asNoTracking: true)
            .AnyAsync(x => x.Name == normalizedName && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim();
        return await Queryable(asNoTracking: true)
            .AnyAsync(x => x.Code == normalizedCode && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task AddAsync(ToolChangeEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.AddAsync(entity, cancellationToken);
    }

    public override Task UpdateAsync(ToolChangeEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.UpdateAsync(entity, cancellationToken);
    }
}