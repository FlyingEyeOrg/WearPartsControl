using System;

namespace WearPartsControl.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
