namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartMonitorService
{
    Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default);
}