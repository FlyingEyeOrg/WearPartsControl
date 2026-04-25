using System;
using System.Collections.Generic;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Exceptions;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class ClientAppConfigurationEntity :
    Entity,
    IHasAuditTime,
    IHasAuditUser
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

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

    public bool EnableCutterMesValidation { get; set; }

    public string CutterMesWsdl { get; set; } = string.Empty;

    public string CutterMesUser { get; set; } = string.Empty;

    public string CutterMesPassword { get; set; } = string.Empty;

    public string CutterMesSite { get; set; } = string.Empty;

    public int SiemensRack { get; set; }

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

        if (SiemensRack < 0 || SiemensRack > 255)
        {
            throw new DomainValidationException("西门子 PLC 机架号必须在 0 到 255 之间。")
                .WithData(nameof(SiemensRack), SiemensRack);
        }

        if (SiemensSlot < 0 || SiemensSlot > 255)
        {
            throw new DomainValidationException("西门子 PLC 插槽号必须在 0 到 255 之间。")
                .WithData(nameof(SiemensSlot), SiemensSlot);
        }

        if (EnableCutterMesValidation)
        {
            DomainValidationRules.NotWhiteSpace(CutterMesWsdl, nameof(CutterMesWsdl));
            DomainValidationRules.NotWhiteSpace(CutterMesUser, nameof(CutterMesUser));
            DomainValidationRules.NotWhiteSpace(CutterMesPassword, nameof(CutterMesPassword));
            DomainValidationRules.NotWhiteSpace(CutterMesSite, nameof(CutterMesSite));
        }
    }
}
