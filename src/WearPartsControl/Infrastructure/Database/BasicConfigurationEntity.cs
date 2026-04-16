using System;
using System.Collections.Generic;

namespace WearPartsControl.Infrastructure.Database;

public sealed class BasicConfigurationEntity
{
    public Guid Id { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string FactoryCode { get; set; } = string.Empty;

    public string AreaCode { get; set; } = string.Empty;

    public string ProcedureCode { get; set; } = string.Empty;

    public string EquipmentCode { get; set; } = "000";

    public string DataStorageTypeCode { get; set; } = "2";

    public string ResourceNumber { get; set; } = string.Empty;

    public string PlcProtocolType { get; set; } = string.Empty;

    public string PlcIpAddress { get; set; } = string.Empty;

    public int PlcPort { get; set; }

    public string ShutdownPointAddress { get; set; } = string.Empty;

    public int SiemensSlot { get; set; }

    public bool IsStringReverse { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WearPartDefinitionEntity> WearPartDefinitions { get; set; } = new List<WearPartDefinitionEntity>();
}
