using System;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class WearPartDefinitionEntity :
    Entity,
    IHasAuditTime,
    IHasAuditUser
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

    public Guid ClientAppConfigurationId { get; set; }

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

    public Guid? WearPartTypeId { get; set; }

    public Guid? ToolChangeId { get; set; }

    public string PlcZeroClearAddress { get; set; } = string.Empty;

    public string BarcodeWriteAddress { get; set; } = string.Empty;

    public ClientAppConfigurationEntity ClientAppConfiguration { get; set; } = null!;

    public WearPartTypeEntity? WearPartType { get; set; }

    public ToolChangeEntity? ToolChange { get; set; }

    public ICollection<WearPartReplacementRecordEntity> WearPartReplacementRecords { get; set; } = new List<WearPartReplacementRecordEntity>();

    public ICollection<ExceedLimitRecordEntity> ExceedLimitRecords { get; set; } = new List<ExceedLimitRecordEntity>();

    public void UpdateAddresses(string currentValueAddress, string warningValueAddress, string shutdownValueAddress)
    {
        DomainValidationRules.NotWhiteSpace(currentValueAddress, nameof(currentValueAddress));
        DomainValidationRules.NotWhiteSpace(warningValueAddress, nameof(warningValueAddress));
        DomainValidationRules.NotWhiteSpace(shutdownValueAddress, nameof(shutdownValueAddress));

        CurrentValueAddress = currentValueAddress.Trim();
        WarningValueAddress = warningValueAddress.Trim();
        ShutdownValueAddress = shutdownValueAddress.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureValid()
    {
        DomainValidationRules.NotWhiteSpace(ResourceNumber, nameof(ResourceNumber));
        DomainValidationRules.NotWhiteSpace(PartName, nameof(PartName));
        DomainValidationRules.NotWhiteSpace(InputMode, nameof(InputMode));
        DomainValidationRules.NotWhiteSpace(CurrentValueAddress, nameof(CurrentValueAddress));
        DomainValidationRules.NotWhiteSpace(CurrentValueDataType, nameof(CurrentValueDataType));
        DomainValidationRules.NotWhiteSpace(WarningValueAddress, nameof(WarningValueAddress));
        DomainValidationRules.NotWhiteSpace(WarningValueDataType, nameof(WarningValueDataType));
        DomainValidationRules.NotWhiteSpace(ShutdownValueAddress, nameof(ShutdownValueAddress));
        DomainValidationRules.NotWhiteSpace(ShutdownValueDataType, nameof(ShutdownValueDataType));
        DomainValidationRules.NotWhiteSpace(LifetimeType, nameof(LifetimeType));
    }
}
