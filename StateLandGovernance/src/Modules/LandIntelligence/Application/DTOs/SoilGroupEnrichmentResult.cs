namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum SoilGroupEnrichmentStatus
{
    Available = 1,
    Unavailable = 2
}

public sealed class SoilGroupOverlapEvidence
{
    public required Guid SoilGroupId { get; init; }

    public required string SoilGroupName { get; init; }

    public required double OverlapAreaSquareMeters { get; init; }

    public required decimal OverlapPercentage { get; init; }
}

public sealed class SoilGroupEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public string? StoredSoilType { get; init; }

    public AttributeProvenanceDto? StoredSoilTypeProvenance { get; init; }

    public required bool OfficialSoilTypePreserved { get; init; }

    public string? PrimarySoilGroup { get; init; }

    public Guid? PrimarySoilGroupId { get; init; }

    public decimal? OverlapPercentage { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public required SoilGroupEnrichmentStatus Status { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required IReadOnlyList<SoilGroupOverlapEvidence> Overlaps { get; init; }

    public required string SourceName { get; init; }

    public required string SourceLayer { get; init; }

    public AttributeProvenanceDto? SoilGroupSourceProvenance { get; init; }

    public AttributeProvenanceDto? DerivedSoilGroupProvenance { get; init; }
}
