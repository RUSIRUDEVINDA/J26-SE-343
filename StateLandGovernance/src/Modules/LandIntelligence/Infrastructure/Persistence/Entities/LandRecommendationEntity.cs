using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class LandRecommendationEntity
{
    public Guid Id { get; set; }

    public Guid LandParcelId { get; set; }

    public decimal SuitabilityScore { get; set; }

    public int? Rank { get; set; }

    public RecommendationStatus Status { get; set; }

    public LandUseType? RecommendedUseType { get; set; }

    public string? RecommendedUseDescription { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public string CriteriaJson { get; set; } = "[]";

    public string EvidenceJson { get; set; } = "[]";
}
