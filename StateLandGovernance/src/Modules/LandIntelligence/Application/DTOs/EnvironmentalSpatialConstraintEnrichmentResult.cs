namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum EnvironmentalSpatialConstraintEnrichmentStatus
{
    Available = 1,
    Unavailable = 2
}

public enum ErosionDataStatus
{
    Available = 1,
    Unavailable = 2
}

public sealed class SoilConservationAreaEvidence
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public double? OverlapAreaSquareMeters { get; init; }

    public decimal? OverlapPercentage { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }
}

public sealed class SoilErosionObservationEvidence
{
    public required Guid Id { get; init; }

    public string? ObservationClass { get; init; }

    public string? Description { get; init; }

    public decimal? ErosionRate { get; init; }

    public double? DistanceMeters { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }
}

public sealed class EnvironmentalSpatialConstraintEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public required EnvironmentalSpatialConstraintEnrichmentStatus Status { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required bool IntersectsSoilConservationArea { get; init; }

    public required IReadOnlyList<SoilConservationAreaEvidence> ConservationAreas { get; init; }

    public required ErosionDataStatus ErosionDataStatus { get; init; }

    public required IReadOnlyList<SoilErosionObservationEvidence> ErosionObservations { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public AttributeProvenanceDto? ConservationAreaSourceProvenance { get; init; }

    public AttributeProvenanceDto? DerivedConservationProvenance { get; init; }

    public AttributeProvenanceDto? ErosionObservationSourceProvenance { get; init; }

    public AttributeProvenanceDto? DerivedErosionProvenance { get; init; }
}
