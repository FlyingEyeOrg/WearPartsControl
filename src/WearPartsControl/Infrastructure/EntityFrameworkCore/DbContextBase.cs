using Microsoft.EntityFrameworkCore;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public abstract class DbContextBase : DbContext
{
    protected DbContextBase(DbContextOptions options) : base(options)
    {
    }
}
