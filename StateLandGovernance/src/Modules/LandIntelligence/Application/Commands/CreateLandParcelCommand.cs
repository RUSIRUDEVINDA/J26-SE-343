using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed record CreateLandParcelCommand(
    string CadastralNumber,
    string? SurveyPlanReference,
    LandCategoryType CategoryType,
    string? CategoryDescription,
    decimal AreaValue,
    AreaUnit AreaUnit,
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision,
    double CentroidLatitude,
    double CentroidLongitude,
    string CoordinateSystem,
    string? BoundaryReference,
    LandUseType? CurrentUseType,
    string? CurrentUseDescription,
    string? SoilType,
    string? TerrainDescription,
    decimal? ElevationMeters);
