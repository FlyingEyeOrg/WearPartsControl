namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartAlertPopupService
{
    ValueTask ShowIfNeededAsync(string title, string markdown, DateTime occurredAt, CancellationToken cancellationToken = default);
}