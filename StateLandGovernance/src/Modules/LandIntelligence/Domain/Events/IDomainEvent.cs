namespace StateLandGovernance.LandIntelligence.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
