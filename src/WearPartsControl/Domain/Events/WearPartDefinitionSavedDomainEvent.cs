using System;

namespace WearPartsControl.Domain.Events;

public sealed record WearPartDefinitionSavedDomainEvent(
    Guid WearPartDefinitionId,
    Guid BasicConfigurationId,
    string PartName,
    DateTime OccurredAtUtc) : IDomainEvent;
