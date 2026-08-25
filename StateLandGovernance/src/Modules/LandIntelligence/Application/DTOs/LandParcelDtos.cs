using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record ParcelIdentifierDto(
    string CadastralNumber,
    string? SurveyPlanReference);

public sealed record AdministrativeLocationDto(
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision);

public sealed record LandAreaDto(
    decimal Value,
    AreaUnit Unit);

public sealed record SpatialReferenceDto(
    double CentroidLatitude,
    double CentroidLongitude,
    string CoordinateSystem,
    string? BoundaryReference,
    GeoJsonPolygonDto? BoundaryPolygon = null);

public sealed record LandCharacteristicsDto(
    string? SoilType,
    string? TerrainDescription,
    decimal? ElevationMeters,
    AttributeProvenanceDto? SoilTypeProvenance = null,
    AttributeProvenanceDto? TerrainDescriptionProvenance = null,
    AttributeProvenanceDto? ElevationMetersProvenance = null);

public sealed record LandCategoryDto(
    LandCategoryType Type,
    string? Description);

public sealed record LandUseDto(
    LandUseType Type,
    string? Description);
