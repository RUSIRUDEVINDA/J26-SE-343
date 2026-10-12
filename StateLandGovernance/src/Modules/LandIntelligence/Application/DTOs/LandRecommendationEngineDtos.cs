using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Input for the explainable land recommendation engine.
/// Criteria thresholds are configurable — no legal rules are embedded.
/// </summary>
public sealed record LandRecommendationSearchRequest
{
    public required LandUseType RequiredPurpose { get; init; }
    /// <summary>Target area (ha) for continuous area-match scoring in Find Suitable Land.</summary>
    public decimal? TargetAreaHectares { get; init; }
    /// <summary>Legacy alias — treated as <see cref="TargetAreaHectares"/> when target is unset (not a minimum filter).</summary>
    public decimal? RequiredAreaHectares { get; init; }
    /// <summary>Legacy alias — treated as <see cref="TargetAreaHectares"/> when target and required are unset.</summary>
    public decimal? PreferredAreaHectares { get; init; }
    /// <summary>Deprecated — no longer applied to area scoring (continuous target match is used instead).</summary>
    public decimal AreaTolerancePercent { get; init; } = 15m;
    public PreferredLocationCriteria? PreferredLocation { get; init; }
    /// <summary>Legacy domain category filter — use only for synthetic/domain-managed parcels.</summary>
    public LandCategoryType? RequiredLandCategory { get; init; }
    public LandUseType? RequiredLandUse { get; init; }
    /// <summary>Exact match on source-reported land-use code or English label (real cadastre).</summary>
    public string? SourceReportedLandUse { get; init; }
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
    string? AttributePath = null,
    bool WasAssessed = true,
    string? NotAssessedReason = null);

public sealed record RecommendationFilterStageDto(
    string Name,
    int CandidateCount,
    string Description);

public sealed record RecommendationUnavailableCriterionDto(
    string Key,
    string Reason);

public sealed record RecommendationSearchTransparencyDto(
    IReadOnlyList<string> AppliedFilters,
    int CandidateCountBeforeFilters,
    int CandidateCountAfterFilters,
    IReadOnlyList<RecommendationFilterStageDto> FilterStages,
    IReadOnlyList<RecommendationUnavailableCriterionDto> UnavailableCriteria);

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
    string Explanation,
    bool HardConstraintRejected = false,
    string? HardConstraintReason = null,
    /// <summary>State-land screening status label (not legal availability).</summary>
    string? CandidateStatusLabel = null,
    string? CandidateStatusReason = null,
    string? SourceReportedClaimantCategoryCode = null,
    string? SourceReportedClaimantCategoryDisplay = null,
    string? SourceParcelStatusCode = null,
    string? SourceProvenanceLabel = null,
    DateTimeOffset? SourceImportedAtUtc = null,
    string? ProjectLeaseReadyLabel = null,
    bool IsProjectLeaseReadyDesignated = false,
    bool IsProjectLeaseReadyEffective = false,
    decimal? ParcelAreaHectares = null,
    int DisplayOrder = 0,
    int RuleScoreRank = 0,
    int RuleScoreTieCount = 0,
    bool IsTiedOnRuleScore = false);

public sealed record LandRecommendationSearchResponse(
    LandUseType RequiredPurpose,
    IReadOnlyList<LandParcelRecommendationResult> Recommendations,
    int CandidateCount,
    DateTimeOffset GeneratedAt,
    RecommendationSearchTransparencyDto? Transparency = null);
