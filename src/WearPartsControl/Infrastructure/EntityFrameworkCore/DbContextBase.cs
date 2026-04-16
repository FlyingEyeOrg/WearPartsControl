using Microsoft.EntityFrameworkCore;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public abstract class DbContextBase : DbContext
{
    protected DbContextBase(DbContextOptions options, IServiceProvider? applicationServiceProvider = null) : base(options)
    {
        ApplicationServiceProvider = applicationServiceProvider;
    }

    public IServiceProvider? ApplicationServiceProvider { get; }
}
