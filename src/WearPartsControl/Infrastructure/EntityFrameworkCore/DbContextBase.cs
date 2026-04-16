using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public abstract class DbContextBase : DbContext, IUnitOfWork<DbContextBase>
{
    private IDbContextTransaction? _currentTransaction;

    protected DbContextBase(DbContextOptions options) : base(options)
    {
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
