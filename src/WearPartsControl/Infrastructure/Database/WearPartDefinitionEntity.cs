using System;

namespace WearPartsControl.Infrastructure.Database;

public sealed class WearPartDefinitionEntity
{
    public Guid Id { get; set; }

    public Guid BasicConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string InputMode { get; set; } = string.Empty;

    public string CurrentValueAddress { get; set; } = string.Empty;

    public string CurrentValueDataType { get; set; } = string.Empty;

    public string WarningValueAddress { get; set; } = string.Empty;

    public string WarningValueDataType { get; set; } = string.Empty;

    public string ShutdownValueAddress { get; set; } = string.Empty;

    public string ShutdownValueDataType { get; set; } = string.Empty;

    public bool IsShutdown { get; set; }

    public int CodeMinLength { get; set; }

    public int CodeMaxLength { get; set; }

    public string LifetimeType { get; set; } = string.Empty;

    public string PlcZeroClearAddress { get; set; } = string.Empty;

    public string BarcodeWriteAddress { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public BasicConfigurationEntity BasicConfiguration { get; set; } = null!;
}
