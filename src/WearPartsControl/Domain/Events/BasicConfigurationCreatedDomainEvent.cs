using System;

namespace WearPartsControl.Domain.Events;

public sealed record BasicConfigurationCreatedDomainEvent(
    Guid BasicConfigurationId,
    string ResourceNumber,
    DateTime OccurredAtUtc) : IDomainEvent;
