namespace WearPartsControl.ViewModels;

public sealed class PartReplacementHistoryExportRequestedEventArgs : EventArgs
{
    public PartReplacementHistoryExportRequestedEventArgs(string suggestedFileName, string content)
    {
        SuggestedFileName = suggestedFileName;
        Content = content;
    }

    public string SuggestedFileName { get; }

    public string Content { get; }
}