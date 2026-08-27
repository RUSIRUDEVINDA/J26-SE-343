using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
