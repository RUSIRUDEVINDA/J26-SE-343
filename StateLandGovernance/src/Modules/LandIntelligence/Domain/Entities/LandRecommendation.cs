using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Events;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public sealed class LandRecommendation : Entity
{
    private readonly List<RecommendationCriterion> _criteria = [];
    private readonly List<RecommendationEvidence> _evidence = [];

    public Guid LandParcelId { get; private set; }
    public decimal SuitabilityScore { get; private set; }
    public int? Rank { get; private set; }
    public RecommendationStatus Status { get; private set; }
    public LandUse? RecommendedUse { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }

    public IReadOnlyCollection<RecommendationCriterion> Criteria => _criteria.AsReadOnly();
    public IReadOnlyCollection<RecommendationEvidence> Evidence => _evidence.AsReadOnly();

    private LandRecommendation()
    {
    }

    public LandRecommendation(
        Guid landParcelId,
        decimal suitabilityScore,
        LandUse? recommendedUse = null,
        RecommendationStatus status = RecommendationStatus.Draft)
    {
        if (landParcelId == Guid.Empty)
        {
            throw new ArgumentException("Land parcel identifier is required.", nameof(landParcelId));
        }

        if (suitabilityScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(suitabilityScore),
                "Suitability score must be between 0 and 100.");
        }

        LandParcelId = landParcelId;
        SuitabilityScore = suitabilityScore;
        RecommendedUse = recommendedUse;
        Status = status;
        GeneratedAt = DateTimeOffset.UtcNow;
    }

    public void AssignRank(int rank)
    {
        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank));
        }

        Rank = rank;
    }

    public void FinalizeRecommendation()
    {
        Status = RecommendationStatus.Final;
        AddDomainEvent(new LandRecommendationGeneratedEvent(Id, LandParcelId, SuitabilityScore));
    }

    public void Supersede() => Status = RecommendationStatus.Superseded;

    public void AddCriterion(RecommendationCriterion criterion) => _criteria.Add(criterion);

    public void AddEvidence(RecommendationEvidence evidenceItem) => _evidence.Add(evidenceItem);

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
