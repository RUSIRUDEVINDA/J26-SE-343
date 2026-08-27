using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.LeaseFeasibility.Domain.Events;

public sealed record FinancialFeasibilityAssessmentFinalizedEvent(
    Guid AssessmentId,
    string ApplicationId,
    FeasibilityGrade Grade
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
