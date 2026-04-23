using System;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class WearPartReplacementRecordEntity : Entity, IHasAuditTime, IHasAuditUser, ISoftDelete
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid ClientAppConfigurationId { get; set; }

    public Guid WearPartDefinitionId { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string PartName { get; set; } = string.Empty;

    public string? CurrentBarcode { get; set; }

    public string NewBarcode { get; set; } = string.Empty;

    public string CurrentValue { get; set; } = string.Empty;

    public string WarningValue { get; set; } = string.Empty;

    public string ShutdownValue { get; set; } = string.Empty;

    public string OperatorWorkNumber { get; set; } = string.Empty;

    public string OperatorUserName { get; set; } = string.Empty;

    public string ReplacementReason { get; set; } = string.Empty;

    public string ReplacementMessage { get; set; } = string.Empty;

    public DateTime ReplacedAt { get; set; } = DateTime.UtcNow;

    public string? DataType { get; set; }

    public string? DataValue { get; set; }

    public ClientAppConfigurationEntity ClientAppConfiguration { get; set; } = null!;

    public WearPartDefinitionEntity WearPartDefinition { get; set; } = null!;

    public void EnsureValid()
    {
        DomainValidationRules.NotWhiteSpace(SiteCode, nameof(SiteCode));
        DomainValidationRules.NotWhiteSpace(PartName, nameof(PartName));
        DomainValidationRules.NotWhiteSpace(NewBarcode, nameof(NewBarcode));
        DomainValidationRules.NotWhiteSpace(CurrentValue, nameof(CurrentValue));
        DomainValidationRules.NotWhiteSpace(WarningValue, nameof(WarningValue));
        DomainValidationRules.NotWhiteSpace(ShutdownValue, nameof(ShutdownValue));
        DomainValidationRules.NotWhiteSpace(OperatorWorkNumber, nameof(OperatorWorkNumber));
        DomainValidationRules.NotWhiteSpace(OperatorUserName, nameof(OperatorUserName));
        DomainValidationRules.NotWhiteSpace(ReplacementReason, nameof(ReplacementReason));
    }
}