namespace WearPartsControl.ViewModels;

public sealed class WearPartMonitoringLogExportRequestedEventArgs : EventArgs
{
    public WearPartMonitoringLogExportRequestedEventArgs(string suggestedFileName, string content)
    {
        SuggestedFileName = suggestedFileName;
        Content = content;
    }

    public string SuggestedFileName { get; }

    public string Content { get; }
}