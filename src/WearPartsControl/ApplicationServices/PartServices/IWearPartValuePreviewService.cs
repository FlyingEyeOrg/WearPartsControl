namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartValuePreviewService
{
    Task<IReadOnlyList<WearPartValuePreviewItem>> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);
}