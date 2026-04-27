namespace WearPartsControl.ViewModels;

public sealed record ReplacePartConfirmationContext(
    string PartName,
    string Barcode,
    bool IsReturningOldPart,
    bool HasReachedWarningLifetime,
    string CurrentValueText,
    string WarningValueText,
    string ShutdownValueText)
{
    public static ReplacePartConfirmationContext Empty { get; } = new(string.Empty, string.Empty, false, false, string.Empty, string.Empty, string.Empty);
}