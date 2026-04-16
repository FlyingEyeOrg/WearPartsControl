using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public abstract class EfRepositoryBase<TDbContext, TEntity, TId> : IRepository<TEntity, TId>
    where TDbContext : DbContextBase
    where TEntity : class, IEntity<TId>
    where TId : notnull
{
    protected EfRepositoryBase(TDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected TDbContext DbContext { get; }

    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    protected IQueryable<TEntity> Queryable(bool asNoTracking = true, bool includeSoftDeleted = false)
    {
        IQueryable<TEntity> queryable = Set;

        if (asNoTracking)
        {
            queryable = queryable.AsNoTracking();
        }

        if (!includeSoftDeleted)
        {
            queryable = ApplySoftDeleteFilter(queryable);
        }

        return queryable;
    }

    protected abstract Expression<Func<TEntity, bool>> BuildIdPredicate(TId id);

    public virtual Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return Queryable(asNoTracking: true)
            .FirstOrDefaultAsync(BuildIdPredicate(id), cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking: true)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        SetCreationDefaults(entity);
        await Set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        SetUpdateDefaults(entity);
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await Queryable(asNoTracking: false)
            .FirstOrDefaultAsync(BuildIdPredicate(id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        SetDeleteDefaults(entity);
        Set.Update(entity);
    }

    protected virtual void SetCreationDefaults(TEntity entity)
    {
        var now = DateTime.UtcNow;

        if (EqualityComparer<TId>.Default.Equals(entity.Id, default!))
        {
            if (typeof(TId) == typeof(Guid))
            {
                entity.Id = (TId)(object)Guid.NewGuid();
            }
        }

        if (entity is IHasAuditTime auditTime)
        {
            if (auditTime.CreatedAt == default)
            {
                auditTime.CreatedAt = now;
            }

            auditTime.UpdatedAt = now;
        }

        if (entity is IHasAuditUser auditUser)
        {
            if (string.IsNullOrWhiteSpace(auditUser.CreatedBy))
            {
                auditUser.CreatedBy = "system";
            }

            if (string.IsNullOrWhiteSpace(auditUser.UpdatedBy))
            {
                auditUser.UpdatedBy = auditUser.CreatedBy;
            }
        }
    }

    protected virtual void SetUpdateDefaults(TEntity entity)
    {
        if (entity is IHasAuditTime auditTime)
        {
            auditTime.UpdatedAt = DateTime.UtcNow;
        }

        if (entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = false;
            softDelete.DeletedAt = null;
        }

        if (entity is IHasAuditUser auditUser && string.IsNullOrWhiteSpace(auditUser.UpdatedBy))
        {
            auditUser.UpdatedBy = "system";
        }
    }

    protected virtual void SetDeleteDefaults(TEntity entity)
    {
        if (entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = true;
            softDelete.DeletedAt = DateTime.UtcNow;

            if (entity is IHasAuditTime auditTime)
            {
                auditTime.UpdatedAt = softDelete.DeletedAt.Value;
            }
        }

        if (entity is IHasAuditUser auditUser && string.IsNullOrWhiteSpace(auditUser.UpdatedBy))
        {
            auditUser.UpdatedBy = "system";
        }
    }

    private static IQueryable<TEntity> ApplySoftDeleteFilter(IQueryable<TEntity> source)
    {
        if (!typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            return source;
        }

        return source.Where(x => !EF.Property<bool>(x, nameof(ISoftDelete.IsDeleted)));
    }
}
