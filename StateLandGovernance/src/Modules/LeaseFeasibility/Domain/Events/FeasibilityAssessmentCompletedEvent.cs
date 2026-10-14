using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Events;

public sealed record FeasibilityAssessmentCompletedEvent(
    Guid AssessmentId,
    string ApplicationId,
    string EligibilityGrade) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
