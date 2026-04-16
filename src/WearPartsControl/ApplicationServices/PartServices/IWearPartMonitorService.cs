namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartMonitorService
{
    Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default);
}