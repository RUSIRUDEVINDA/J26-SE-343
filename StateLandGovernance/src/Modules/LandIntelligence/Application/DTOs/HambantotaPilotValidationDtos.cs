using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Request to validate the Component 1 Hambantota pilot pipeline for a single parcel scenario.
/// </summary>
public sealed record HambantotaPilotValidationRequest
{
    public required Guid ParcelId { get; init; }

    public required string ScenarioKey { get; init; }

    public required string ScenarioDescription { get; init; }

    public bool UsesSyntheticGisData { get; init; }

    public IReadOnlyList<LandUseType> LandUsePurposes { get; init; } = [];

    public decimal? MaxRoadDistanceMeters { get; init; } = 20_000m;

    public bool RequireRoadAccess { get; init; } = true;

    public bool SyncToKnowledgeGraph { get; init; } = true;
}

public sealed record HambantotaPilotValidationRecommendationResult
{
    public required LandUseType RequestedPurpose { get; init; }

    public int? FinalRank { get; init; }

    public decimal? SuitabilityScore { get; init; }

    public bool IsRecommended { get; init; }

    public IReadOnlyList<CriterionEvaluationDto> MatchingCriteria { get; init; } = [];

    public IReadOnlyList<CriterionEvaluationDto> FailedCriteria { get; init; } = [];

    public IReadOnlyList<RestrictionSummaryDto> Restrictions { get; init; } = [];

    public IReadOnlyList<RecommendationEvidenceDto> Evidence { get; init; } = [];

    public string? Explanation { get; init; }
}

public sealed record HambantotaPilotValidationKnowledgeGraphResult
{
    public required string Status { get; init; }

    public bool? HasRoadRelationship { get; init; }

    public bool? HasWaterRelationship { get; init; }

    public bool? HasDerivedSoilRelationship { get; init; }

    public bool? HasConservationRelationship { get; init; }

    public string? NearestWaterFeatureType { get; init; }

    public string? Notes { get; init; }
}

public sealed record HambantotaPilotValidationBehaviourChecks
{
    public bool OfficialSoilTypeUnchanged { get; init; }

    public bool OfficialProvinceUnchanged { get; init; }

    public bool OfficialDistrictUnchanged { get; init; }

    public bool NaturalWaterNotTreatedAsWaterSupply { get; init; }

    public bool GisSoilSeparateFromOfficialSoil { get; init; }

    public bool ConservationNotDoubleCounted { get; init; }

    public bool MissingErosionNotInterpretedAsSafety { get; init; }

    public bool IdempotentEnrichment { get; init; }

    public bool IdempotentPersistence { get; init; }

    public bool IdempotentKnowledgeGraphSync { get; init; }

    public bool NoFabricatedEvidenceWhenUnavailable { get; init; }
}

public sealed record HambantotaPilotValidationMetrics
{
    public required Guid ParcelId { get; init; }

    public required string CadastralNumber { get; init; }

    public required string ScenarioKey { get; init; }

    public required string ScenarioDescription { get; init; }

    public bool UsesSyntheticGisData { get; init; }

    public required string GisEnrichmentOverallStatus { get; init; }

    public string? DetectedProvince { get; init; }

    public string? DetectedDistrict { get; init; }

    public decimal? MappedRoadDistanceMeters { get; init; }

    public string? MappedRoadName { get; init; }

    public decimal? MappedWaterDistanceMeters { get; init; }

    public string? MappedWaterFeatureType { get; init; }

    public string? OfficialSoilType { get; init; }

    public string? GisDerivedSoilGroup { get; init; }

    public bool? ConservationIntersection { get; init; }

    public int? ConservationAreaCount { get; init; }

    public required string ErosionDataAvailability { get; init; }

    public IReadOnlyList<string> EnrichmentEvidence { get; init; } = [];

    public IReadOnlyList<string> EnrichmentWarnings { get; init; } = [];

    public IReadOnlyList<string> EnrichmentFailures { get; init; } = [];

    public IReadOnlyList<string> ProvenanceSources { get; init; } = [];
}

public sealed record HambantotaPilotValidationScenarioResult
{
    public required HambantotaPilotValidationMetrics Metrics { get; init; }

    public IReadOnlyList<HambantotaPilotValidationRecommendationResult> Recommendations { get; init; } = [];

    public HambantotaPilotValidationKnowledgeGraphResult? KnowledgeGraph { get; init; }

    public required HambantotaPilotValidationBehaviourChecks BehaviourChecks { get; init; }
}

public sealed record HambantotaPilotValidationSummary
{
    public int ScenarioCount { get; init; }

    public int ScenariosWithAvailableGis { get; init; }

    public int ScenariosWithPartialGis { get; init; }

    public int ScenariosWithUnavailableGis { get; init; }

    public int RecommendationRuns { get; init; }

    public int RecommendationsProduced { get; init; }

    public int BehaviourChecksPassed { get; init; }

    public int BehaviourChecksTotal { get; init; }
}

public sealed record HambantotaPilotValidationReport
{
    public required string PilotDistrict { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public bool Neo4jAvailable { get; init; }

    public IReadOnlyList<HambantotaPilotValidationScenarioResult> Scenarios { get; init; } = [];

    public required HambantotaPilotValidationSummary Summary { get; init; }
}
