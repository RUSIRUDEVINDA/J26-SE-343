namespace StateLandGovernance.BuildingBlocks.Events;

using System;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
