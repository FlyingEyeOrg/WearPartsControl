using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.EntityFrameworkCore;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
