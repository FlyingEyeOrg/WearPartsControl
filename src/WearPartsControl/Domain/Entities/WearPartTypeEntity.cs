using System;
using WearPartsControl.Domain.Entities.Interfaces;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Entities;

public sealed class WearPartTypeEntity : Entity, IHasAuditTime, IHasAuditUser
{
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; } = string.Empty;

    public string? UpdatedBy { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<WearPartDefinitionEntity> WearPartDefinitions { get; set; } = new List<WearPartDefinitionEntity>();

    public void EnsureValid()
    {
        DomainValidationRules.NotWhiteSpace(Code, nameof(Code));
        DomainValidationRules.NotWhiteSpace(Name, nameof(Name));
    }
}