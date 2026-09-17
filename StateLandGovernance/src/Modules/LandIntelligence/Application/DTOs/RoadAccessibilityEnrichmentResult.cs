namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum RoadAccessibilityEnrichmentStatus
{
    Available = 1,
    Unavailable = 2
}

public sealed class RoadAccessibilityEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public Guid? RoadId { get; init; }

    public string? RoadName { get; init; }

    public GisReferenceRoadType? RoadType { get; init; }

    public double? DistanceMeters { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required RoadAccessibilityEnrichmentStatus Status { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public AttributeProvenanceDto? RoadSourceProvenance { get; init; }

    public AttributeProvenanceDto? DistanceProvenance { get; init; }
}
