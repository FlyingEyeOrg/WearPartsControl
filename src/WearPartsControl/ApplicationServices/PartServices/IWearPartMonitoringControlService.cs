namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartMonitoringControlService
{
    Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken = default);

    Task EnableAsync(CancellationToken cancellationToken = default);

    Task DisableAsync(CancellationToken cancellationToken = default);
}