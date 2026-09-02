namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum LandParcelGisEnrichmentOverallStatus
{
    Complete = 1,
    Partial = 2,
    Unavailable = 3
}

public sealed class LandParcelGisEnrichmentSectionFailure
{
    public required string Section { get; init; }

    public required string Message { get; init; }
}

public sealed class LandParcelGisEnrichmentResult
{
    public required Guid ParcelId { get; init; }

    public required string CadastralNumber { get; init; }

    public required LandParcelGisEnrichmentOverallStatus OverallStatus { get; init; }

    public AdministrativeLocationGeometryBasis? GeometryBasis { get; init; }

    public AdministrativeLocationVerificationResult? Administrative { get; init; }

    public RoadAccessibilityEnrichmentResult? RoadAccessibility { get; init; }

    public WaterProximityEnrichmentResult? WaterProximity { get; init; }

    public SoilGroupEnrichmentResult? Soil { get; init; }

    public EnvironmentalSpatialConstraintEnrichmentResult? Environmental { get; init; }

    public required IReadOnlyList<string> Evidence { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public required IReadOnlyList<LandParcelGisEnrichmentSectionFailure> Failures { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
