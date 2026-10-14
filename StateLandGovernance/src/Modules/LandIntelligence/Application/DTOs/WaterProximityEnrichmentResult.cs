namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum WaterProximityEnrichmentStatus
{
    Available = 1,
    /// <summary>Inside coverage but no canal/lake source data (or geometry missing).</summary>
    Unavailable = 2,
    /// <summary>Parcel is outside configured GIS enrichment district coverage.</summary>
    OutsideCoverage = 3
}

public sealed class WaterProximityEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public Guid? FeatureId { get; init; }

    public string? FeatureName { get; init; }

    public GisReferenceWaterFeatureType? FeatureType { get; init; }

    public double? DistanceMeters { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required WaterProximityEnrichmentStatus Status { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public AttributeProvenanceDto? FeatureSourceProvenance { get; init; }

    public AttributeProvenanceDto? DistanceProvenance { get; init; }
}
