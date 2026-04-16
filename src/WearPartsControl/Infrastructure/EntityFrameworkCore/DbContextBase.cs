using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public abstract class DbContextBase : DbContext, IUnitOfWork
{
    protected DbContextBase(DbContextOptions options) : base(options)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
