using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record LandRecommendationRequest
{
    public Guid LandParcelId { get; init; }
    public LandUseType? TargetLandUseType { get; init; }
    public string? TargetLandUseDescription { get; init; }
    public bool FinalizeRecommendation { get; init; }
}

public sealed record RecommendationCriterionDto(
    CriterionCategory Category,
    string Name,
    decimal Weight,
    decimal Score,
    string? Summary,
    decimal WeightedScore);

public sealed record RecommendationEvidenceDto(
    string Source,
    string Description,
    string? RelatedCriterionName,
    AttributeProvenanceDto? DataProvenance = null);

public sealed record LandRecommendationDto(
    Guid Id,
    Guid LandParcelId,
    decimal SuitabilityScore,
    int? Rank,
    RecommendationStatus Status,
    LandUseDto? RecommendedUse,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RecommendationCriterionDto> Criteria,
    IReadOnlyList<RecommendationEvidenceDto> Evidence);
