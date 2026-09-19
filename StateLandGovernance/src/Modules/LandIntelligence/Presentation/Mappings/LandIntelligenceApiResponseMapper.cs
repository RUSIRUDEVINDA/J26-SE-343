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
            dto.RegulatoryReferenceCount);

    public static LandSearchResultsResponse ToResponse(LandSearchResponse response) =>
        new(
            response.Results.Select(ToSearchResultResponse).ToList(),
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
            response.GeneratedAt);

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
            dto.CentroidLongitude);

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
            dto.HardConstraintReason);

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
            dto.AttributePath);

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
