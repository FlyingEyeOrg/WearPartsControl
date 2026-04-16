using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
