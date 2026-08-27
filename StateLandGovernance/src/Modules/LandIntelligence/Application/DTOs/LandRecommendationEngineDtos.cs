using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Input for the explainable land recommendation engine.
/// Criteria thresholds are configurable — no legal rules are embedded.
/// </summary>
public sealed record LandRecommendationSearchRequest
{
    public required LandUseType RequiredPurpose { get; init; }
    public decimal? RequiredAreaHectares { get; init; }
    public decimal AreaTolerancePercent { get; init; } = 15m;
    public PreferredLocationCriteria? PreferredLocation { get; init; }
    public LandCategoryType? RequiredLandCategory { get; init; }
    public LandUseType? RequiredLandUse { get; init; }
    public AccessibilityCriteria? Accessibility { get; init; }
    public EnvironmentalCriteria? Environmental { get; init; }
    public RegulatoryCriteria? Regulatory { get; init; }
    public IReadOnlyList<CustomCriterionCriteria>? AdditionalCriteria { get; init; }
    public Guid? TargetParcelId { get; init; }
    public int MaxResults { get; init; } = 10;
}

public sealed record PreferredLocationCriteria
{
    public string? Province { get; init; }
    public string? District { get; init; }
    public string? DivisionalSecretariat { get; init; }
}

public sealed record AccessibilityCriteria
{
    public decimal? MaxRoadDistanceMeters { get; init; }
    public bool RequireRoadAccess { get; init; }
}

public sealed record EnvironmentalCriteria
{
    public RestrictionSeverity MaxAllowedEnvironmentalSeverity { get; init; } = RestrictionSeverity.Medium;
    public bool RejectProhibitiveEnvironmentalRestrictions { get; init; } = true;
}

public sealed record RegulatoryCriteria
{
    public int MaxRegulatoryReferences { get; init; } = 5;
    public bool PenalizeMultipleReferences { get; init; } = true;
}

public sealed record CustomCriterionCriteria
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public decimal Weight { get; init; } = 0.05m;
    public required string ExpectedValue { get; init; }
    public string? ParcelAttributePath { get; init; }
}

public sealed record CriterionEvaluationDto(
    string Key,
    string Name,
    CriterionCategory Category,
    bool IsMet,
    decimal Score,
    decimal Weight,
    decimal WeightedScore,
    string Summary,
    AttributeProvenanceDto? DataProvenance = null,
    string? AttributePath = null);

public sealed record RestrictionSummaryDto(
    string RestrictionType,
    string Description,
    RestrictionSeverity Severity,
    string Source,
    AttributeProvenanceDto? DataProvenance = null);

public sealed record LandParcelRecommendationResult(
    Guid ParcelId,
    string CadastralNumber,
    decimal SuitabilityScore,
    int Rank,
    IReadOnlyList<CriterionEvaluationDto> MatchingCriteria,
    IReadOnlyList<CriterionEvaluationDto> FailedCriteria,
    IReadOnlyList<RestrictionSummaryDto> Restrictions,
    IReadOnlyList<RecommendationEvidenceDto> Evidence,
    string Explanation);

public sealed record LandRecommendationSearchResponse(
    LandUseType RequiredPurpose,
    IReadOnlyList<LandParcelRecommendationResult> Recommendations,
    int CandidateCount,
    DateTimeOffset GeneratedAt);
