using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public interface IUnitOfWork<TDbContext> : IUnitOfWork
    where TDbContext : DbContext
{
    TDbContext DbContext { get; }
}
