using Microsoft.EntityFrameworkCore.Storage;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class EfUnitOfWork<TDbContext> : IUnitOfWork<TDbContext>
    where TDbContext : DbContextBase
{
    private IDbContextTransaction? _currentTransaction;

    public EfUnitOfWork(TDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public TDbContext DbContext { get; }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return;
        }

        _currentTransaction = await DbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await _currentTransaction.DisposeAsync().ConfigureAwait(false);
        _currentTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        await _currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await _currentTransaction.DisposeAsync().ConfigureAwait(false);
        _currentTransaction = null;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }
    }
}
