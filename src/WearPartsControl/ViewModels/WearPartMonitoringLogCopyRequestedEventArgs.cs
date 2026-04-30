namespace WearPartsControl.ViewModels;

public sealed class WearPartMonitoringLogCopyRequestedEventArgs : EventArgs
{
    public WearPartMonitoringLogCopyRequestedEventArgs(string content)
    {
        Content = content;
    }

    public string Content { get; }
}