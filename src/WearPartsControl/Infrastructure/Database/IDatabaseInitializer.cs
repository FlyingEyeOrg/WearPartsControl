using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.Infrastructure.Database;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
