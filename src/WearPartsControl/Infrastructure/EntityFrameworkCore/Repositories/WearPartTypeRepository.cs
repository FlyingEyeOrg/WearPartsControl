using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public sealed class WearPartTypeRepository : EfRepositoryBase<WearPartsControlDbContext, WearPartTypeEntity, Guid>, IWearPartTypeRepository
{
    public WearPartTypeRepository(WearPartsControlDbContext dbContext)
        : base(dbContext)
    {
    }

    protected override Expression<Func<WearPartTypeEntity, bool>> BuildIdPredicate(Guid id) => x => x.Id == id;

    public override async Task<IReadOnlyList<WearPartTypeEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<WearPartTypeEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code?.Trim() ?? string.Empty;
        return Queryable(asNoTracking: true)
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
    }

    public override Task AddAsync(WearPartTypeEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.AddAsync(entity, cancellationToken);
    }

    public override Task UpdateAsync(WearPartTypeEntity entity, CancellationToken cancellationToken = default)
    {
        entity.EnsureValid();
        return base.UpdateAsync(entity, cancellationToken);
    }
}