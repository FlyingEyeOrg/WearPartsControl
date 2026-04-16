using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class ExceedLimitRecordEntity : Entity, IHasAuditTime, IHasAuditUser, IHasRemark
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public Guid BasicConfigurationId { get; set; }

    public Guid WearPartDefinitionId { get; set; }

    public string PartName { get; set; } = string.Empty;

    public double CurrentValue { get; set; }

    public double WarningValue { get; set; }

    public double ShutdownValue { get; set; }

    public string Severity { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string NotificationMessage { get; set; } = string.Empty;

    public BasicConfigurationEntity BasicConfiguration { get; set; } = null!;

    public WearPartDefinitionEntity WearPartDefinition { get; set; } = null!;

    public void EnsureValid()
    {
        DomainValidationRules.NotWhiteSpace(PartName, nameof(PartName));
        DomainValidationRules.NotWhiteSpace(Severity, nameof(Severity));
    }
}