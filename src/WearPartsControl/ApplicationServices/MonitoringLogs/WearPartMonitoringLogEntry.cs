namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public sealed record WearPartMonitoringLogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    WearPartMonitoringLogLevel Level,
    WearPartMonitoringLogCategory Category,
    string Message,
    string? OperationName = null,
    string? ResourceNumber = null,
    string? Address = null,
    string? Details = null);