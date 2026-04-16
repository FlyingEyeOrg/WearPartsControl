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
    protected EfRepositoryBase(TDbContext dbContext, ICurrentUser currentUser)
    {
        DbContext = dbContext;
        CurrentUser = currentUser;
    }

    protected TDbContext DbContext { get; }

    protected ICurrentUser CurrentUser { get; }

    protected string? CurrentUserId => CurrentUser.UserId;

    protected string EffectiveUserId => string.IsNullOrWhiteSpace(CurrentUserId) ? "system" : CurrentUserId;

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
        var entity = await Queryable(asNoTracking: false, includeSoftDeleted: true)
            .FirstOrDefaultAsync(BuildIdPredicate(id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        ApplyDeleteBehavior(entity);
    }

    protected virtual void SetCreationDefaults(TEntity entity)
    {
        var now = DateTime.UtcNow;

        if (typeof(TId) == typeof(Guid) && ((Guid)(object)entity.Id) == Guid.Empty)
        {
            entity.Id = (TId)(object)Guid.NewGuid();
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
            if (ShouldAssignCurrentUser(auditUser.CreatedBy))
            {
                auditUser.CreatedBy = EffectiveUserId;
            }

            auditUser.UpdatedBy = EffectiveUserId;
        }
    }

    protected virtual void SetUpdateDefaults(TEntity entity)
    {
        if (entity is IHasAuditTime auditTime)
        {
            auditTime.UpdatedAt = DateTime.UtcNow;
        }

        if (entity is IHasAuditUser auditUser)
        {
            auditUser.UpdatedBy = EffectiveUserId;
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

        if (entity is IHasAuditUser auditUser)
        {
            auditUser.UpdatedBy = EffectiveUserId;
        }
    }

    protected virtual void ApplyDeleteBehavior(TEntity entity)
    {
        if (entity is ISoftDelete)
        {
            SetDeleteDefaults(entity);
            Set.Update(entity);
            return;
        }

        Set.Remove(entity);
    }

    private static IQueryable<TEntity> ApplySoftDeleteFilter(IQueryable<TEntity> source)
    {
        if (!typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            return source;
        }

        return source.Where(x => !EF.Property<bool>(x, nameof(ISoftDelete.IsDeleted)));
    }

    private static bool ShouldAssignCurrentUser(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId)
               || string.Equals(userId, "system", StringComparison.OrdinalIgnoreCase);
    }
}
