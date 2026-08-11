using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed record UpdateLandParcelCommand(
    Guid LandParcelId,
    LandUseType? CurrentUseType,
    string? CurrentUseDescription,
    string? SoilType,
    string? TerrainDescription,
    decimal? ElevationMeters);
