namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public sealed record WearPartMonitoringLogQuery(
    WearPartMonitoringLogLevel? Level,
    WearPartMonitoringLogCategory? Category,
    string? Keyword,
    int Offset,
    int Limit);