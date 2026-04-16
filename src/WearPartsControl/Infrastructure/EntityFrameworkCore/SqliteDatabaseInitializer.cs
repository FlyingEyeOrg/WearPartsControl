using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<WearPartsControlDbContext> _dbContextFactory;

    public SqliteDatabaseInitializer(IDbContextFactory<WearPartsControlDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
