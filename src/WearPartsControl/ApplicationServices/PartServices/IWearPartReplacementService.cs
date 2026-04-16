namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartReplacementService
{
    Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WearPartReplacementRecord>> GetReplacementHistoryAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default);
}