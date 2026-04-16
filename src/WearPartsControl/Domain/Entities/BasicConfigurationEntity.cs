using System;
using System.Collections.Generic;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Exceptions;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class BasicConfigurationEntity :
    Entity,
    IHasAuditTime,
    IHasAuditUser,
    ISoftDelete,
    IHasRemark
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? Remark { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string FactoryCode { get; set; } = string.Empty;

    public string AreaCode { get; set; } = string.Empty;

    public string ProcedureCode { get; set; } = string.Empty;

    public string EquipmentCode { get; set; } = string.Empty;

    public string ResourceNumber { get; set; } = string.Empty;

    public string PlcProtocolType { get; set; } = string.Empty;

    public string PlcIpAddress { get; set; } = string.Empty;

    public int PlcPort { get; set; }

    public string ShutdownPointAddress { get; set; } = string.Empty;

    public int SiemensSlot { get; set; }

    public bool IsStringReverse { get; set; } = true;

    public ICollection<WearPartDefinitionEntity> WearPartDefinitions { get; set; } = new List<WearPartDefinitionEntity>();

    public ICollection<WearPartReplacementRecordEntity> WearPartReplacementRecords { get; set; } = new List<WearPartReplacementRecordEntity>();

    public ICollection<ExceedLimitRecordEntity> ExceedLimitRecords { get; set; } = new List<ExceedLimitRecordEntity>();

    public void UpdatePlcConnection(string protocolType, string ipAddress, int port)
    {
        DomainValidationRules.NotWhiteSpace(protocolType, nameof(protocolType));
        DomainValidationRules.NotWhiteSpace(ipAddress, nameof(ipAddress));

        if (port <= 0 || port > 65535)
        {
            throw new DomainValidationException("PLC 端口号必须在 1 到 65535 之间。")
                .WithData(nameof(port), port);
        }

        PlcProtocolType = protocolType.Trim();
        PlcIpAddress = ipAddress.Trim();
        PlcPort = port;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureValid()
    {
        DomainValidationRules.NotWhiteSpace(SiteCode, nameof(SiteCode));
        DomainValidationRules.NotWhiteSpace(FactoryCode, nameof(FactoryCode));
        DomainValidationRules.NotWhiteSpace(AreaCode, nameof(AreaCode));
        DomainValidationRules.NotWhiteSpace(ProcedureCode, nameof(ProcedureCode));
        DomainValidationRules.NotWhiteSpace(EquipmentCode, nameof(EquipmentCode));
        DomainValidationRules.NotWhiteSpace(ResourceNumber, nameof(ResourceNumber));
        DomainValidationRules.NotWhiteSpace(PlcProtocolType, nameof(PlcProtocolType));
        DomainValidationRules.NotWhiteSpace(PlcIpAddress, nameof(PlcIpAddress));

        if (PlcPort <= 0 || PlcPort > 65535)
        {
            throw new DomainValidationException("PLC 端口号必须在 1 到 65535 之间。")
                .WithData(nameof(PlcPort), PlcPort);
        }
    }
}
