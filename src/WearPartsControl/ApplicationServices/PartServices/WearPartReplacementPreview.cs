namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementPreview
{
    public Guid WearPartDefinitionId { get; set; }

    public Guid BasicConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string? LastBarcode { get; set; }

    public string CurrentValue { get; set; } = string.Empty;

    public string WarningValue { get; set; } = string.Empty;

    public string ShutdownValue { get; set; } = string.Empty;
}