namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Experimental Colombo model assessment status. Distinct from
/// <see cref="Domain.Enums.GisEnrichmentOverallStatus"/> and must not replace it.
/// </summary>
public enum ExperimentalGisAssessmentStatus
{
    AssessedComplete = 1,
    AssessedPartial = 2,
    Unavailable = 3
}

/// <summary>
/// Payload shaped for the inactive Colombo experimental RF feature schema.
/// Not sent to ml_service.py unless a future activation step is explicitly implemented.
/// </summary>
public sealed class ExperimentalColomboMlFeaturePayload
{
    public required string RequestedPurpose { get; init; }

    public required string LandCategory { get; init; }

    public required double AreaHectares { get; init; }

    public double? DistanceToRoadM { get; init; }

    public double? DistanceToWaterM { get; init; }

    public required string GisEnrichmentStatus { get; init; }

    public required string DerivedSoilGroup { get; init; }

    public required string? EnvironmentalRestrictionType { get; init; }

    public required string? EnvironmentalRestrictionSeverity { get; init; }

    public required bool? SpatialConstraintPresent { get; init; }

    /// <summary>
    /// False when required GIS evidence for the backend-compatible candidate is missing
    /// (e.g. conservation assessment unavailable). Callers must not treat this as a class prediction.
    /// </summary>
    public required bool ExperimentalPredictionSupported { get; init; }

    public string? ExperimentalPredictionAbstentionReason { get; init; }

    public required IReadOnlyList<string> GisDerivedFields { get; init; }

    public required IReadOnlyList<string> BackendActualFields { get; init; }

    public required IReadOnlyList<string> SimulatedOrUnavailableFields { get; init; }

    public required IReadOnlyList<string> RemainingMismatches { get; init; }

    public required string RoadSourceLayer { get; init; }

    public string? RoadHighwayClass { get; init; }

    public string? RoadOsmId { get; init; }

    public string? RoadFilterPolicyVersion { get; init; }

    public required bool ModelActivationEnabled { get; init; }
}

public sealed class OsmMotorRoadImportResult
{
    public required int FeaturesImported { get; init; }

    public required int FeaturesUpdated { get; init; }

    public required int FeaturesSkipped { get; init; }

    public required int TotalOsmMotorRoadsInTable { get; init; }

    public required string SourceLayer { get; init; }

    public required string FilterPolicyVersion { get; init; }

    public required string SourcePath { get; init; }

    public required IReadOnlyList<string> SkipLog { get; init; }

    public required string CoverageNote { get; init; }
}
