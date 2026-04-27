namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartTypeService
{
    Task<IReadOnlyList<WearPartTypeDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
}