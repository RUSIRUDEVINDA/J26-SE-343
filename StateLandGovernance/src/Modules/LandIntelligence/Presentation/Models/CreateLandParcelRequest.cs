using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Presentation.Models;

public sealed record CreateLandParcelRequest
{
    public string CadastralNumber { get; init; } = string.Empty;
    public string? SurveyPlanReference { get; init; }
    public LandCategoryType CategoryType { get; init; }
    public string? CategoryDescription { get; init; }
    public decimal AreaValue { get; init; }
    public AreaUnit AreaUnit { get; init; } = AreaUnit.Hectares;
    public string Province { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public string DivisionalSecretariat { get; init; } = string.Empty;
    public string? GramaNiladhariDivision { get; init; }
    public double CentroidLatitude { get; init; }
    public double CentroidLongitude { get; init; }
    public string CoordinateSystem { get; init; } = "EPSG:4326";
    public string? BoundaryReference { get; init; }
    public GeoJsonPolygonDto? BoundaryPolygon { get; init; }
    public LandUseType? CurrentUseType { get; init; }
    public string? CurrentUseDescription { get; init; }
    public LandCharacteristicsInputDto? Characteristics { get; init; }
    public IReadOnlyList<SpatialConstraintInputDto>? SpatialConstraints { get; init; }
    public IReadOnlyList<EnvironmentalRestrictionInputDto>? EnvironmentalRestrictions { get; init; }
    public IReadOnlyList<InfrastructureFeatureInputDto>? InfrastructureFeatures { get; init; }
    public IReadOnlyList<RegulatoryReferenceInputDto>? RegulatoryReferences { get; init; }
}
