namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartThresholdService
{
    Task<WearPartThresholdProfile> GetProfileAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    Task<WearPartThresholdProfile> UpdateThresholdsAsync(WearPartThresholdUpdateRequest request, CancellationToken cancellationToken = default);

    Task<WearPartThresholdPlcSnapshot> ReadPlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    Task<WearPartThresholdPlcSnapshot> OverwritePlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);
}