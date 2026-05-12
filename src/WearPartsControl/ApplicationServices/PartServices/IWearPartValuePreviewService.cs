namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartValuePreviewService
{
    Task<IReadOnlyList<WearPartValuePreviewItem>> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WearPartValuePreviewItem>> SyncConfiguredThresholdsToDeviceAsync(string resourceNumber, CancellationToken cancellationToken = default);
}