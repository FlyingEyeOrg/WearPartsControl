using System;
using System.Collections.Generic;

namespace WearPartsControl.Domain.Entities;

public sealed class BasicConfigurationEntity
{
    public Guid Id { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string FactoryCode { get; set; } = string.Empty;

    public string AreaCode { get; set; } = string.Empty;

    public string ProcedureCode { get; set; } = string.Empty;

    public string EquipmentCode { get; set; } = string.Empty;

    public string ResourceNumber { get; set; } = string.Empty;

    public string PlcProtocolType { get; set; } = string.Empty;

    public string PlcIpAddress { get; set; } = string.Empty;

    public int PlcPort { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WearPartDefinitionEntity> WearPartDefinitions { get; set; } = new List<WearPartDefinitionEntity>();
}
