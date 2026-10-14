namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record LandCharacteristics
{
    public string? SoilType { get; }
    public string? TerrainDescription { get; }
    public decimal? ElevationMeters { get; }
    public AttributeProvenance? SoilTypeProvenance { get; }
    public AttributeProvenance? TerrainDescriptionProvenance { get; }
    public AttributeProvenance? ElevationMetersProvenance { get; }

    public LandCharacteristics(
        string? soilType = null,
        string? terrainDescription = null,
        decimal? elevationMeters = null,
        AttributeProvenance? soilTypeProvenance = null,
        AttributeProvenance? terrainDescriptionProvenance = null,
        AttributeProvenance? elevationMetersProvenance = null)
    {
        SoilType = string.IsNullOrWhiteSpace(soilType) ? null : soilType.Trim();
        TerrainDescription = string.IsNullOrWhiteSpace(terrainDescription)
            ? null
            : terrainDescription.Trim();
        ElevationMeters = elevationMeters is < 0 ? null : elevationMeters;
        SoilTypeProvenance = soilTypeProvenance;
        TerrainDescriptionProvenance = terrainDescriptionProvenance;
        ElevationMetersProvenance = elevationMetersProvenance;
    }
}
