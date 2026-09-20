using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Events;

public sealed record LeaseProposalGeneratedEvent(
    Guid ProposalId,
    string ApplicationId,
    decimal OptimizationConfidence) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
