namespace WearPartsControl.ApplicationServices.MonitoringLogs;

public sealed class WearPartMonitoringLogEntriesAddedEventArgs : EventArgs
{
    public WearPartMonitoringLogEntriesAddedEventArgs(IReadOnlyList<WearPartMonitoringLogEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<WearPartMonitoringLogEntry> Entries { get; }
}