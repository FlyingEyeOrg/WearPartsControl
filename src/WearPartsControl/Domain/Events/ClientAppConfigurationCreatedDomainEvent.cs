using System;

namespace WearPartsControl.Domain.Events;

public sealed record ClientAppConfigurationCreatedDomainEvent(
    Guid ClientAppConfigurationId,
    string ResourceNumber,
    DateTime OccurredAtUtc) : IDomainEvent;
