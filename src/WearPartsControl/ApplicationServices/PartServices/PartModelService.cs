using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class PartModelService : IPartModelService
{
    private readonly ISaveInfoStore _saveInfoStore;

    public PartModelService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask<IReadOnlyList<BaseFactoryModel>> GetBaseFactoryModelsAsync(CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<BaseFactoryOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);

        return options.Factories
            .Where(x => !string.IsNullOrWhiteSpace(x.Base) && !string.IsNullOrWhiteSpace(x.FactoryName))
            .ToArray();
    }

    public ValueTask SaveBaseFactoryModelsAsync(IReadOnlyCollection<BaseFactoryModel> factories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factories);

        var options = new BaseFactoryOptionsSaveInfo
        {
            Factories = factories
                .Where(x => x is not null)
                .Select(x => new BaseFactoryModel
                {
                    Base = x.Base,
                    BaseName = x.BaseName,
                    FactoryName = x.FactoryName
                })
                .ToList()
        };

        return _saveInfoStore.WriteAsync(options, cancellationToken);
    }
}
