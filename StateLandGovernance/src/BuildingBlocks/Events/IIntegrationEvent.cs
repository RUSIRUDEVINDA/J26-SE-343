namespace StateLandGovernance.BuildingBlocks.Events;

using System;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
