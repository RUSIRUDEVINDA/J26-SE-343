using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed record CreateLandParcelCommand
{
    public required string CadastralNumber { get; init; }
    public string? SurveyPlanReference { get; init; }
    public required LandCategoryType CategoryType { get; init; }
    public string? CategoryDescription { get; init; }
    public required decimal AreaValue { get; init; }
    public AreaUnit AreaUnit { get; init; } = AreaUnit.Hectares;
    public required string Province { get; init; }
    public required string District { get; init; }
    public required string DivisionalSecretariat { get; init; }
    public string? GramaNiladhariDivision { get; init; }
    public required double CentroidLatitude { get; init; }
    public required double CentroidLongitude { get; init; }
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
