using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore.Repositories;

public abstract class EfRepositoryBase<TDbContext, TEntity, TId> : IRepository<TEntity, TId>
    where TDbContext : DbContextBase
    where TEntity : class, IEntity<TId>
    where TId : notnull
{
    protected EfRepositoryBase(TDbContext dbContext, IUnitOfWork<TDbContext> unitOfWork)
    {
        DbContext = dbContext;
        UnitOfWork = unitOfWork;
        ServiceProvider = dbContext.GetService<IServiceProvider>();
        IServiceProvider? tempServiceProvider = ServiceProvider;

        if (tempServiceProvider == null)
        {
            CurrentUser = new DefaultCurrentUser();
        }
        else
        {
            CurrentUser = ServiceProvider.GetService<ICurrentUser>() ?? new DefaultCurrentUser();
        }
    }

    protected IServiceProvider ServiceProvider { get; }

    protected TDbContext DbContext { get; }

    public IUnitOfWork UnitOfWork { get; }

    protected ICurrentUser CurrentUser { get; }

    protected string? CurrentUserId => CurrentUser.UserId;

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

        Set.Remove(entity);
    }

    public virtual async Task SoftDeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await Queryable(asNoTracking: false, includeSoftDeleted: true)
            .FirstOrDefaultAsync(BuildIdPredicate(id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        if (entity is not ISoftDelete)
        {
            Set.Remove(entity);
            return;
        }

        SetDeleteDefaults(entity);
        Set.Update(entity);
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
            if (ShouldAssignCurrentUser(auditUser.CreatedBy) && HasCurrentUser())
            {
                auditUser.CreatedBy = CurrentUserId;
            }

            if (HasCurrentUser())
            {
                auditUser.UpdatedBy = CurrentUserId;
            }
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
            if (HasCurrentUser())
            {
                auditUser.UpdatedBy = CurrentUserId;
            }
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
            if (HasCurrentUser())
            {
                auditUser.UpdatedBy = CurrentUserId;
            }
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

    private static bool ShouldAssignCurrentUser(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId);
    }

    private bool HasCurrentUser()
    {
        return !string.IsNullOrWhiteSpace(CurrentUserId);
    }
}
