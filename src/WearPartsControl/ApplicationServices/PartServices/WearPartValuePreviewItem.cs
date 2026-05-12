namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartValuePreviewItem
{
    public Guid WearPartDefinitionId { get; set; }

    public Guid ClientAppConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string WearPartTypeName { get; set; } = string.Empty;

    public string LifetimeType { get; set; } = string.Empty;

    public double CurrentValue { get; set; }

    public double WarningValue { get; set; }

    public double ShutdownValue { get; set; }

    public WearPartMonitorStatus Status { get; set; }
}