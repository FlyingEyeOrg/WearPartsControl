namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public interface IWearPartMonitoringLogPipeline
{
    event EventHandler<WearPartMonitoringLogEntriesAddedEventArgs>? EntriesAdded;

    event EventHandler? Cleared;

    int Capacity { get; }

    int RetainedCount { get; }

    IReadOnlyList<WearPartMonitoringLogEntry> Snapshot();

    WearPartMonitoringLogPage Query(WearPartMonitoringLogQuery query);

    void Publish(
        WearPartMonitoringLogLevel level,
        WearPartMonitoringLogCategory category,
        string message,
        string? operationName = null,
        string? resourceNumber = null,
        string? address = null,
        string? details = null,
        Exception? exception = null);

    void Clear();
}