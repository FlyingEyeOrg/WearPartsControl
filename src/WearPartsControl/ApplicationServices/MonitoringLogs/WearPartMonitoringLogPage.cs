namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public sealed record WearPartMonitoringLogPage(
    IReadOnlyList<WearPartMonitoringLogEntry> Entries,
    int TotalCount,
    int Offset,
    int Limit,
    int RetainedCount);