namespace WearPartsControl.ApplicationServices.PartServices;

public sealed record MonitoringRuntimeState(bool IsClientAppInfoConfigured, string ResourceNumber, bool IsWearPartMonitoringEnabled)
{
    public bool HasResourceNumber => !string.IsNullOrWhiteSpace(ResourceNumber);

    public bool CanWarmUpPlcOnStartup => IsClientAppInfoConfigured && HasResourceNumber && IsWearPartMonitoringEnabled;

    public bool CanRunBackgroundMonitoring => CanWarmUpPlcOnStartup;
}