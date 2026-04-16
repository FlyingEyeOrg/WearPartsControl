using System;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class WearPartDefinitionEntity :
    Entity,
    IHasAuditTime,
    IHasAuditUser,
    ISoftDelete,
    IHasRemark
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = "system";

    public string UpdatedBy { get; set; } = "system";

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? Remark { get; set; }

    public Guid BasicConfigurationId { get; set; }

    public string ResourceNumber { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string CurrentValueAddress { get; set; } = string.Empty;

    public string WarningValueAddress { get; set; } = string.Empty;

    public string ShutdownValueAddress { get; set; } = string.Empty;

    public BasicConfigurationEntity BasicConfiguration { get; set; } = null!;

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
        DomainValidationRules.NotWhiteSpace(CurrentValueAddress, nameof(CurrentValueAddress));
        DomainValidationRules.NotWhiteSpace(WarningValueAddress, nameof(WarningValueAddress));
        DomainValidationRules.NotWhiteSpace(ShutdownValueAddress, nameof(ShutdownValueAddress));
    }
}
