namespace StateLandGovernance.LandIntelligence.Domain.Events;

public sealed record LandRecommendationGeneratedEvent(
    Guid RecommendationId,
    Guid LandParcelId,
    decimal SuitabilityScore) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
