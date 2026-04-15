using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.PartServices;

public interface IPartModelService
{
    ValueTask<IReadOnlyList<BaseFactoryModel>> GetBaseFactoryModelsAsync(CancellationToken cancellationToken = default);

    ValueTask SaveBaseFactoryModelsAsync(IReadOnlyCollection<BaseFactoryModel> factories, CancellationToken cancellationToken = default);
}
