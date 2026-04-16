using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.PartServices;

public interface IAppSettingsService
{
    ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}