using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

public sealed record ParcelIdentifierResponse(
    string CadastralNumber,
    string? SurveyPlanReference);

public sealed record AdministrativeLocationResponse(
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision);

public sealed record LandAreaResponse(
    decimal Value,
    AreaUnit Unit);

/// <summary>
/// GeoJSON polygon coordinates for API consumers. Raw PostGIS/NTS geometries are never exposed.
/// </summary>
public sealed record GeoJsonPolygonResponse
{
    public string Type { get; init; } = "Polygon";

    public required IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates { get; init; }
}

public sealed record SpatialReferenceResponse(
    double CentroidLatitude,
    double CentroidLongitude,
    string CoordinateSystem,
    string? BoundaryReference,
    GeoJsonPolygonResponse? BoundaryPolygon = null);

public sealed record AttributeProvenanceResponse(
    AttributeProvenanceSourceType SourceType,
    string SourceName,
    decimal? Confidence,
    DateTimeOffset? CollectedAt,
    bool IsVerified);

public sealed record LandCharacteristicsResponse(
    string? SoilType,
    string? TerrainDescription,
    decimal? ElevationMeters,
    AttributeProvenanceResponse? SoilTypeProvenance = null,
    AttributeProvenanceResponse? TerrainDescriptionProvenance = null,
    AttributeProvenanceResponse? ElevationMetersProvenance = null);

public sealed record LandCategoryResponse(
    LandCategoryType Type,
    string? Description);

public sealed record LandUseResponse(
    LandUseType Type,
    string? Description);

/// <summary>
/// Source-reported cadastral metadata for real imported parcels. Does not include ownership or claimant fields.
/// </summary>
public sealed record CadastralSourceMetadataResponse(
    string SourceProvenanceLabel,
    string SourceLayer,
    string SourceFeatureId,
    string? SourceReportedLandUseCode,
    string? SourceReportedLandUseEnglishLabel,
    string? SourceReportedLandUseText,
    string SourceReportedLandUseDisplay,
    string? SourceReportedClaimantCategoryCode,
    string? SourceReportedClaimantCategoryEnglishLabel,
    string SourceReportedClaimantCategoryDisplay,
    string? SourceParcelStatusCode,
    string? GnIndex,
    string? CoordinateStatus,
    DateTimeOffset ImportedAtUtc,
    string? ResearchRecordId = null,
    double? MapMarkerLatitude = null,
    double? MapMarkerLongitude = null);

public sealed record ResearchSimulatedLeaseWorkflowResponse(
    string ResearchRecordId,
    string ScenarioId,
    string SimulatedLeaseStatus,
    bool IsSimulatedLeaseStatus,
    string? DatasetUsage,
    string? Notice);

public sealed record LandParcelAvailabilityResponse(
    string ActualLeaseAvailabilityLabel,
    string? ResearchRecordId,
    ResearchSimulatedLeaseWorkflowResponse? SimulatedWorkflow);

public sealed record ResearchDemoConfigResponse(bool Enabled);

public sealed record SourceReportedLandUseOptionResponse(
    string Code,
    string EnglishLabel);

public sealed record LandParcelResponse(
    Guid Id,
    ParcelIdentifierResponse Identifier,
    LandCategoryResponse Category,
    LandUseResponse? CurrentUse,
    LandAreaResponse Area,
    AdministrativeLocationResponse Location,
    SpatialReferenceResponse Spatial,
    LandCharacteristicsResponse? Characteristics,
    int SpatialConstraintCount,
    int EnvironmentalRestrictionCount,
    int InfrastructureFeatureCount,
    int RegulatoryReferenceCount,
    CadastralSourceMetadataResponse? CadastralSource = null,
    LandParcelAvailabilityResponse? Availability = null);

public sealed record LandSearchResultResponse(
    Guid Id,
    string CadastralNumber,
    string Province,
    string District,
    LandCategoryType CategoryType,
    LandUseType? CurrentUseType,
    decimal AreaValue,
    AreaUnit AreaUnit,
    double CentroidLatitude,
    double CentroidLongitude,
    CadastralSourceMetadataResponse? CadastralSource = null);

public sealed record LandSearchResultsResponse(
    IReadOnlyList<LandSearchResultResponse> Results,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SpatialConstraintResponse(
    Guid Id,
    Guid LandParcelId,
    SpatialConstraintType Type,
    string Description,
    RestrictionSeverity Severity);

public sealed record LandRelationshipResponse(
    string RelationshipType,
    string SourceNodeType,
    string SourceNodeId,
    string TargetNodeType,
    string TargetNodeId,
    string? Description);

public sealed record LandRelationshipsResponse(
    Guid LandParcelId,
    IReadOnlyList<LandRelationshipResponse> Relationships);

public sealed record RecommendationCriterionResponse(
    CriterionCategory Category,
    string Name,
    decimal Weight,
    decimal Score,
    string? Summary,
    decimal WeightedScore);

public sealed record RecommendationEvidenceResponse(
    string Source,
    string Description,
    string? RelatedCriterionName,
    AttributeProvenanceResponse? DataProvenance = null);

public sealed record CriterionEvaluationResponse(
    string Key,
    string Name,
    CriterionCategory Category,
    bool IsMet,
    decimal Score,
    decimal Weight,
    decimal WeightedScore,
    string Summary,
    AttributeProvenanceResponse? DataProvenance = null,
    string? AttributePath = null,
    bool WasAssessed = true,
    string? NotAssessedReason = null);

public sealed record RecommendationFilterStageResponse(
    string Name,
    int CandidateCount,
    string Description);

public sealed record RecommendationUnavailableCriterionResponse(
    string Key,
    string Reason);

public sealed record RecommendationSearchTransparencyResponse(
    IReadOnlyList<string> AppliedFilters,
    int CandidateCountBeforeFilters,
    int CandidateCountAfterFilters,
    IReadOnlyList<RecommendationFilterStageResponse> FilterStages,
    IReadOnlyList<RecommendationUnavailableCriterionResponse> UnavailableCriteria);

public sealed record RestrictionSummaryResponse(
    string RestrictionType,
    string Description,
    RestrictionSeverity Severity,
    string Source,
    AttributeProvenanceResponse? DataProvenance = null);

public sealed record LandParcelRecommendationResponse(
    Guid ParcelId,
    string CadastralNumber,
    decimal SuitabilityScore,
    int Rank,
    IReadOnlyList<CriterionEvaluationResponse> MatchingCriteria,
    IReadOnlyList<CriterionEvaluationResponse> FailedCriteria,
    IReadOnlyList<RestrictionSummaryResponse> Restrictions,
    IReadOnlyList<RecommendationEvidenceResponse> Evidence,
    string Explanation,
    bool HardConstraintRejected = false,
    string? HardConstraintReason = null,
    string? CandidateStatusLabel = null,
    string? CandidateStatusReason = null,
    string? SourceReportedClaimantCategoryCode = null,
    string? SourceReportedClaimantCategoryDisplay = null,
    string? SourceParcelStatusCode = null,
    string? SourceProvenanceLabel = null,
    DateTimeOffset? SourceImportedAtUtc = null);

public sealed record LandRecommendationSearchResultsResponse(
    LandUseType RequiredPurpose,
    IReadOnlyList<LandParcelRecommendationResponse> Recommendations,
    int CandidateCount,
    DateTimeOffset GeneratedAt,
    RecommendationSearchTransparencyResponse? Transparency = null);

public sealed record LandRecommendationResponse(
    Guid Id,
    Guid LandParcelId,
    decimal SuitabilityScore,
    int? Rank,
    RecommendationStatus Status,
    LandUseResponse? RecommendedUse,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RecommendationCriterionResponse> Criteria,
    IReadOnlyList<RecommendationEvidenceResponse> Evidence);
