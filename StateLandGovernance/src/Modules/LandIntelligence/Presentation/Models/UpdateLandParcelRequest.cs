using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Presentation.Models;

public sealed record UpdateLandParcelRequest
{
    public LandUseType? CurrentUseType { get; init; }

    public string? CurrentUseDescription { get; init; }

    public string? SoilType { get; init; }

    public string? TerrainDescription { get; init; }

    public decimal? ElevationMeters { get; init; }
}
