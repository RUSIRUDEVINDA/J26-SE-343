using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Mappings;

internal static class LandIntelligenceApiResponseMapper
{
    public static LandParcelResponse ToResponse(LandParcelDto dto) =>
        new(
            dto.Id,
            new ParcelIdentifierResponse(dto.Identifier.CadastralNumber, dto.Identifier.SurveyPlanReference),
            new LandCategoryResponse(dto.Category.Type, dto.Category.Description),
            dto.CurrentUse is null ? null : new LandUseResponse(dto.CurrentUse.Type, dto.CurrentUse.Description),
            new LandAreaResponse(dto.Area.Value, dto.Area.Unit),
            new AdministrativeLocationResponse(
                dto.Location.Province,
                dto.Location.District,
                dto.Location.DivisionalSecretariat,
                dto.Location.GramaNiladhariDivision),
            ToSpatialResponse(dto.Spatial),
            dto.Characteristics is null ? null : ToCharacteristicsResponse(dto.Characteristics),
            dto.SpatialConstraintCount,
            dto.EnvironmentalRestrictionCount,
            dto.InfrastructureFeatureCount,
            dto.RegulatoryReferenceCount,
            ToCadastralSourceResponse(dto.CadastralSource),
            ToAvailabilityResponse(dto.Availability),
            ToGisEnrichmentSummaryResponse(dto.GisEnrichment),
            ToGnDivisionMappingResponse(dto.GnDivisionMapping));

    public static LandSearchResultsResponse ToResponse(LandSearchResponse response) =>
        new(
            response.Results.Select(ToSearchResultResponse).ToList(),
            response.Page,
            response.PageSize,
            response.TotalCount);

    public static ApprovedPreliminaryParcelListResponse ToApprovedPreliminaryParcelListResponse(
        Application.DTOs.ApprovedPreliminaryParcelListResponse response) =>
        new(
            response.Results
                .Select(item => new ApprovedPreliminaryParcelSummaryResponse(
                    item.LandParcelId,
                    item.CadastralNumber,
                    item.Province,
                    item.District,
                    item.AreaValue,
                    item.AreaUnit,
                    item.SourceReportedLandUse,
                    item.ApprovedAtUtc,
                    item.LeaseInterestRequestId,
                    item.RequestReference))
                .ToList(),
            response.Page,
            response.PageSize,
            response.TotalCount);

    public static SpatialConstraintResponse ToResponse(SpatialConstraintDto dto) =>
        new(dto.Id, dto.LandParcelId, dto.Type, dto.Description, dto.Severity);

    public static Models.Responses.LandRelationshipsResponse ToResponse(
        Application.DTOs.LandRelationshipsResponse dto) =>
        new(
            dto.LandParcelId,
            dto.Relationships.Select(ToRelationshipResponse).ToList());

    public static LandRecommendationSearchResultsResponse ToResponse(LandRecommendationSearchResponse response) =>
        new(
            response.RequiredPurpose,
            response.Recommendations.Select(ToRecommendationResponse).ToList(),
            response.CandidateCount,
            response.GeneratedAt,
            response.Transparency is null
                ? null
                : new RecommendationSearchTransparencyResponse(
                    response.Transparency.AppliedFilters,
                    response.Transparency.CandidateCountBeforeFilters,
                    response.Transparency.CandidateCountAfterFilters,
                    response.Transparency.FilterStages
                        .Select(s => new RecommendationFilterStageResponse(s.Name, s.CandidateCount, s.Description))
                        .ToList(),
                    response.Transparency.UnavailableCriteria
                        .Select(u => new RecommendationUnavailableCriterionResponse(u.Key, u.Reason))
                        .ToList()));

    public static LandRecommendationResponse ToResponse(LandRecommendationDto dto) =>
        new(
            dto.Id,
            dto.LandParcelId,
            dto.SuitabilityScore,
            dto.Rank,
            dto.Status,
            dto.RecommendedUse is null ? null : new LandUseResponse(dto.RecommendedUse.Type, dto.RecommendedUse.Description),
            dto.GeneratedAt,
            dto.Criteria.Select(ToCriterionResponse).ToList(),
            dto.Evidence.Select(ToEvidenceResponse).ToList());

    public static LandParcelMetricsResponse ToResponse(LandParcelMetricsDto dto) =>
        new(dto.TotalParcelCount, dto.ProjectDesignatedParcelsCount, dto.ProjectDesignatedParcelsEffectiveCount);

    public static LandIntelligenceGisCoverageResponse ToResponse(LandIntelligenceGisCoverageDto dto) =>
        new(
            dto.TotalParcelCount,
            dto.IndexedDistricts
                .Select(item => new IndexedDistrictSummaryResponse(item.Province, item.District, item.ParcelCount))
                .ToList(),
            dto.ConfiguredEnrichmentDistricts,
            dto.EnrichmentSnapshotCount,
            dto.EnrichmentCompleteCount,
            dto.EnrichmentPartialCount,
            dto.EnrichmentUnavailableCount,
            dto.OverallEnrichmentStatus.ToString(),
            dto.OverallEnrichmentNotice,
            dto.IntelligenceLayers.Select(ToGisLayerCoverageResponse).ToList(),
            dto.MeasuredAtUtc);

    private static GisIntelligenceLayerCoverageResponse ToGisLayerCoverageResponse(
        GisIntelligenceLayerCoverageDto dto) =>
        new(
            dto.LayerKey,
            dto.DisplayName,
            dto.CoverageStatus.ToString(),
            dto.ReferenceFeatureCount,
            dto.ParcelsWithEvidenceCount,
            dto.ProvenanceSummary,
            dto.LimitationSummary);

    private static ParcelGisEnrichmentSummaryResponse? ToGisEnrichmentSummaryResponse(
        ParcelGisEnrichmentSummaryDto? dto) =>
        dto is null
            ? null
            : new ParcelGisEnrichmentSummaryResponse(
                dto.OverallStatus.ToString(),
                dto.DetectedProvince,
                dto.DetectedDistrict,
                dto.EnrichedAtUtc,
                dto.AssessmentNotice);

    private static LandSearchResultResponse ToSearchResultResponse(LandSearchResultDto dto) =>
        new(
            dto.Id,
            dto.CadastralNumber,
            dto.Province,
            dto.District,
            dto.CategoryType,
            dto.CurrentUseType,
            dto.AreaValue,
            dto.AreaUnit,
            dto.CentroidLatitude,
            dto.CentroidLongitude,
            ToCadastralSourceResponse(dto.CadastralSource),
            ToAvailabilityResponse(dto.Availability));

    private static ParcelGnDivisionMappingResponse? ToGnDivisionMappingResponse(ParcelGnDivisionMappingDto? dto) =>
        dto is null
            ? null
            : new ParcelGnDivisionMappingResponse(
                dto.Status.ToString(),
                dto.WorkbookStatusLabel,
                dto.ReviewStatusDisplay,
                dto.ProvenanceLabel,
                dto.SourceGnIndex,
                ToGnDivisionAssignmentResponse(dto.PrimaryAssignment),
                ToGnDivisionAssignmentResponse(dto.SourceFromAdministrativeCode),
                ToGnDivisionAssignmentResponse(dto.GnAtMapMarker),
                dto.CodeDistrict,
                dto.MapMarkerLatitude,
                dto.MapMarkerLongitude,
                dto.DatasetKey,
                dto.CadastralNumber,
                dto.ImportedAtUtc);

    private static GnDivisionAdministrativeAssignmentResponse? ToGnDivisionAssignmentResponse(
        GnDivisionAdministrativeAssignmentDto? dto) =>
        dto is null
            ? null
            : new GnDivisionAdministrativeAssignmentResponse(
                dto.GnName,
                dto.GnNumber,
                dto.DivisionalSecretariat);

    private static CadastralSourceMetadataResponse? ToCadastralSourceResponse(CadastralSourceMetadataDto? dto) =>
        dto is null
            ? null
            : new CadastralSourceMetadataResponse(
                dto.SourceProvenanceLabel,
                dto.SourceLayer,
                dto.SourceFeatureId,
                dto.SourceReportedLandUseCode,
                dto.SourceReportedLandUseEnglishLabel,
                dto.SourceReportedLandUseText,
                dto.SourceReportedLandUseDisplay,
                dto.SourceReportedClaimantCategoryCode,
                dto.SourceReportedClaimantCategoryEnglishLabel,
                dto.SourceReportedClaimantCategoryDisplay,
                dto.SourceParcelStatusCode,
                dto.GnIndex,
                dto.CoordinateStatus,
                dto.ImportedAtUtc,
                dto.ResearchRecordId,
                dto.MapMarkerLatitude,
                dto.MapMarkerLongitude);

    private static LandParcelAvailabilityResponse? ToAvailabilityResponse(LandParcelAvailabilityDto? dto) =>
        dto is null
            ? null
            : new LandParcelAvailabilityResponse(
                dto.ActualLeaseAvailabilityLabel,
                dto.ResearchRecordId,
                dto.SimulatedWorkflow is null
                    ? null
                    : new ResearchSimulatedLeaseWorkflowResponse(
                        dto.SimulatedWorkflow.ResearchRecordId,
                        dto.SimulatedWorkflow.ScenarioId,
                        dto.SimulatedWorkflow.SimulatedLeaseStatus,
                        dto.SimulatedWorkflow.IsSimulatedLeaseStatus,
                        dto.SimulatedWorkflow.DatasetUsage,
                        dto.SimulatedWorkflow.Notice),
                ToProjectLeaseReadyResponse(dto.ProjectLeaseReady));

    private static ProjectLeaseReadyDesignationResponse? ToProjectLeaseReadyResponse(
        ProjectLeaseReadyDesignationDto? dto) =>
        dto is null
            ? null
            : new ProjectLeaseReadyDesignationResponse(
                dto.DatasetKey,
                dto.ResearchRecordId,
                dto.DesignationLabel,
                dto.DesignationNotice,
                dto.IsDesignatedInProjectDataset,
                dto.IsEffectiveForLeaseFlow,
                dto.ExcludedBecauseSourceReportedPrivate);

    private static SpatialReferenceResponse ToSpatialResponse(SpatialReferenceDto dto) =>
        new(
            dto.CentroidLatitude,
            dto.CentroidLongitude,
            dto.CoordinateSystem,
            dto.BoundaryReference,
            dto.BoundaryPolygon is null ? null : ToGeoJsonResponse(dto.BoundaryPolygon));

    private static GeoJsonPolygonResponse ToGeoJsonResponse(GeoJsonPolygonDto dto) =>
        new()
        {
            Type = dto.Type,
            Coordinates = dto.Coordinates ?? []
        };

    private static LandCharacteristicsResponse ToCharacteristicsResponse(LandCharacteristicsDto dto) =>
        new(
            dto.SoilType,
            dto.TerrainDescription,
            dto.ElevationMeters,
            ToProvenanceResponse(dto.SoilTypeProvenance),
            ToProvenanceResponse(dto.TerrainDescriptionProvenance),
            ToProvenanceResponse(dto.ElevationMetersProvenance));

    private static AttributeProvenanceResponse? ToProvenanceResponse(AttributeProvenanceDto? dto) =>
        dto is null
            ? null
            : new AttributeProvenanceResponse(
                dto.SourceType,
                dto.SourceName ?? string.Empty,
                dto.Confidence,
                dto.CollectedAt,
                dto.Verified);

    private static LandRelationshipResponse ToRelationshipResponse(LandRelationshipDto dto) =>
        new(
            dto.RelationshipType,
            dto.SourceNodeType,
            dto.SourceNodeId,
            dto.TargetNodeType,
            dto.TargetNodeId,
            dto.Description);

    private static LandParcelRecommendationResponse ToRecommendationResponse(LandParcelRecommendationResult dto) =>
        new(
            dto.ParcelId,
            dto.CadastralNumber,
            dto.SuitabilityScore,
            dto.Rank,
            dto.MatchingCriteria.Select(ToCriterionEvaluationResponse).ToList(),
            dto.FailedCriteria.Select(ToCriterionEvaluationResponse).ToList(),
            dto.Restrictions.Select(ToRestrictionResponse).ToList(),
            dto.Evidence.Select(ToEvidenceResponse).ToList(),
            dto.Explanation,
            dto.HardConstraintRejected,
            dto.HardConstraintReason,
            dto.CandidateStatusLabel,
            dto.CandidateStatusReason,
            dto.SourceReportedClaimantCategoryCode,
            dto.SourceReportedClaimantCategoryDisplay,
            dto.SourceParcelStatusCode,
            dto.SourceProvenanceLabel,
            dto.SourceImportedAtUtc,
            dto.ProjectLeaseReadyLabel,
            dto.IsProjectLeaseReadyDesignated,
            dto.IsProjectLeaseReadyEffective,
            dto.ParcelAreaHectares,
            dto.DisplayOrder,
            dto.RuleScoreRank,
            dto.RuleScoreTieCount,
            dto.IsTiedOnRuleScore,
            dto.ActualLeaseAvailabilityLabel);

    private static CriterionEvaluationResponse ToCriterionEvaluationResponse(CriterionEvaluationDto dto) =>
        new(
            dto.Key,
            dto.Name,
            dto.Category,
            dto.IsMet,
            dto.Score,
            dto.Weight,
            dto.WeightedScore,
            dto.Summary,
            ToProvenanceResponse(dto.DataProvenance),
            dto.AttributePath,
            dto.WasAssessed,
            dto.NotAssessedReason);

    private static RestrictionSummaryResponse ToRestrictionResponse(RestrictionSummaryDto dto) =>
        new(
            dto.RestrictionType,
            dto.Description,
            dto.Severity,
            dto.Source,
            ToProvenanceResponse(dto.DataProvenance));

    private static RecommendationCriterionResponse ToCriterionResponse(RecommendationCriterionDto dto) =>
        new(dto.Category, dto.Name, dto.Weight, dto.Score, dto.Summary, dto.WeightedScore);

    private static RecommendationEvidenceResponse ToEvidenceResponse(RecommendationEvidenceDto dto) =>
        new(
            dto.Source,
            dto.Description,
            dto.RelatedCriterionName,
            ToProvenanceResponse(dto.DataProvenance));
}
