using System;

namespace WearPartsControl.Domain.Entities;

public sealed class WearPartDefinitionEntity
{
    public Guid Id { get; set; }

    public Guid BasicConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string CurrentValueAddress { get; set; } = string.Empty;

    public string WarningValueAddress { get; set; } = string.Empty;

    public string ShutdownValueAddress { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public BasicConfigurationEntity BasicConfiguration { get; set; } = null!;
}
