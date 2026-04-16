namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementRequest
{
    public Guid WearPartDefinitionId { get; set; }

    public string NewBarcode { get; set; } = string.Empty;

    public string ReplacementReason { get; set; } = string.Empty;

    public string ReplacementMessage { get; set; } = string.Empty;
}