using WearPartsControl.Domain.Repositories;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartTypeService : IWearPartTypeService
{
    private readonly IWearPartTypeRepository _wearPartTypeRepository;

    public WearPartTypeService(IWearPartTypeRepository wearPartTypeRepository)
    {
        _wearPartTypeRepository = wearPartTypeRepository;
    }

    public async Task<IReadOnlyList<WearPartTypeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _wearPartTypeRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        return entities
            .Select(x => new WearPartTypeDefinition
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name
            })
            .ToArray();
    }
}