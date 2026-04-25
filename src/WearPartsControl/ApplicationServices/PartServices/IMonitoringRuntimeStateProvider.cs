namespace WearPartsControl.ApplicationServices.PartServices;

public interface IMonitoringRuntimeStateProvider
{
    ValueTask<MonitoringRuntimeState> GetCurrentAsync(CancellationToken cancellationToken = default);
}