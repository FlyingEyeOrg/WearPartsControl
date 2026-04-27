namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolChangeSelectionState
{
    public string SelectedToolCode { get; init; } = string.Empty;

    public IReadOnlyList<string> RecentToolCodes { get; init; } = Array.Empty<string>();
}