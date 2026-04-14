using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.SaveInfoService;

public interface ISaveInfoStore
{
    ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default) where T : class, new();

    ValueTask WriteAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, new();
}
