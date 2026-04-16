using System;

namespace WearPartsControl.Domain.Events;

public sealed record WearPartDefinitionSavedDomainEvent(
    Guid WearPartDefinitionId,
    Guid ClientAppConfigurationId,
    string PartName,
    DateTime OccurredAtUtc) : IDomainEvent;
