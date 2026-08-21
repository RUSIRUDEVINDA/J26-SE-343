using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Presentation.Models;

public sealed record CreateLandParcelRequest
{
    public string CadastralNumber { get; init; } = string.Empty;

    public string? SurveyPlanReference { get; init; }

    public LandCategoryType CategoryType { get; init; }

    public string? CategoryDescription { get; init; }

    public decimal AreaValue { get; init; }

    public AreaUnit AreaUnit { get; init; }

    public string Province { get; init; } = string.Empty;

    public string District { get; init; } = string.Empty;

    public string DivisionalSecretariat { get; init; } = string.Empty;

    public string? GramaNiladhariDivision { get; init; }

    public double CentroidLatitude { get; init; }

    public double CentroidLongitude { get; init; }

    public string CoordinateSystem { get; init; } = "EPSG:4326";

    public string? BoundaryReference { get; init; }

    public LandUseType? CurrentUseType { get; init; }

    public string? CurrentUseDescription { get; init; }

    public string? SoilType { get; init; }

    public string? TerrainDescription { get; init; }

    public decimal? ElevationMeters { get; init; }
}
