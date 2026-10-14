namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;

public sealed record LeaseCaseInitialized : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public string ApplicationReference { get; }
    public Guid ActorId { get; }
    public int LeaseCaseRevision { get; }

    public LeaseCaseInitialized(
        Guid eventId,
        DateTime occurredOn,
        LeaseCaseId leaseCaseId,
        string applicationReference,
        Guid actorId,
        int leaseCaseRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        LeaseCaseId = leaseCaseId;
        ApplicationReference = applicationReference;
        ActorId = actorId;
        LeaseCaseRevision = leaseCaseRevision;
    }
}
