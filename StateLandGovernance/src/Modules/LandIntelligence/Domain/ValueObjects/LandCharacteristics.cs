namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record LandCharacteristics
{
    public string? SoilType { get; }
    public string? TerrainDescription { get; }
    public decimal? ElevationMeters { get; }

    public LandCharacteristics(
        string? soilType = null,
        string? terrainDescription = null,
        decimal? elevationMeters = null)
    {
        SoilType = string.IsNullOrWhiteSpace(soilType) ? null : soilType.Trim();
        TerrainDescription = string.IsNullOrWhiteSpace(terrainDescription)
            ? null
            : terrainDescription.Trim();
        ElevationMeters = elevationMeters is < 0 ? null : elevationMeters;
    }
}
