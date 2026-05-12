namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartThresholdProfile
{
    public Guid WearPartDefinitionId { get; set; }

    public Guid ClientAppConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string LifetimeType { get; set; } = string.Empty;

    public double WarningLifetimeThreshold { get; set; }

    public double ShutdownLifetimeThreshold { get; set; }
}

public sealed class WearPartThresholdUpdateRequest
{
    public Guid WearPartDefinitionId { get; set; }

    public double WarningLifetimeThreshold { get; set; }

    public double ShutdownLifetimeThreshold { get; set; }
}

public sealed class WearPartThresholdPlcSnapshot
{
    public double WarningLifetimeThreshold { get; set; }

    public double ShutdownLifetimeThreshold { get; set; }
}