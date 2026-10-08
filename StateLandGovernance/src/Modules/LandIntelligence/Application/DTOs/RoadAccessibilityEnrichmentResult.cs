namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum RoadAccessibilityEnrichmentStatus
{
    Available = 1,
    /// <summary>Inside coverage but no usable road source data (or geometry missing).</summary>
    Unavailable = 2,
    /// <summary>Parcel is outside configured GIS enrichment district coverage.</summary>
    OutsideCoverage = 3
}

public sealed class RoadAccessibilityEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public Guid? RoadId { get; init; }

    public string? RoadName { get; init; }

    public GisReferenceRoadType? RoadType { get; init; }

    /// <summary>Geodesic metres to nearest filtered road centreline. Never coerced to zero when missing.</summary>
    public double? DistanceMeters { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required RoadAccessibilityEnrichmentStatus Status { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public AttributeProvenanceDto? RoadSourceProvenance { get; init; }

    public AttributeProvenanceDto? DistanceProvenance { get; init; }

    /// <summary>OSM highway class when SourceLayer is osm_motor_roads; otherwise null.</summary>
    public string? HighwayClass { get; init; }

    /// <summary>OSM feature id when imported from osm_motor_roads.</summary>
    public string? OsmId { get; init; }

    /// <summary>Motor-road filter policy version when applicable.</summary>
    public string? FilterPolicyVersion { get; init; }
}
