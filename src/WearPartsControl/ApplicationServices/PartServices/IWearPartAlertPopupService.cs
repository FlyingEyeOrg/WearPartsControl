namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartAlertPopupService
{
    ValueTask ShowIfNeededAsync(Guid wearPartId, string title, string markdown, DateTime occurredAt, CancellationToken cancellationToken = default);
}