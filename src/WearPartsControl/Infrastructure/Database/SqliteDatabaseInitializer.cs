using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.Database;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<PartDbContext> _dbContextFactory;

    public SqliteDatabaseInitializer(IDbContextFactory<PartDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
