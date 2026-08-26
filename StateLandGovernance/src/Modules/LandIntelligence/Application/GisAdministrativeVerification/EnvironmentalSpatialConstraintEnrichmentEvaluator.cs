using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public sealed record SoilConservationOverlapCandidate(
    Guid ConservationAreaId,
    string Name,
    string? Description,
    string SourceName,
    string SourceLayer,
    double OverlapAreaSquareMeters,
    decimal OverlapPercentage);

public sealed record SoilConservationCentroidCandidate(
    Guid ConservationAreaId,
    string Name,
    string? Description,
    string SourceName,
    string SourceLayer);

public sealed record SoilErosionObservationCandidate(
    Guid ObservationId,
    string? ObservationClass,
    string? Description,
    decimal? ErosionRate,
    string SourceName,
    string SourceLayer,
    double DistanceMeters);

public static class EnvironmentalSpatialConstraintEnrichmentEvaluator
{
    public static IReadOnlyList<SoilConservationAreaEvidence> ToConservationAreaEvidence(
        IReadOnlyList<SoilConservationOverlapCandidate> overlaps) =>
        overlaps
            .OrderByDescending(item => item.OverlapAreaSquareMeters)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SoilConservationAreaEvidence
            {
                Id = item.ConservationAreaId,
                Name = item.Name,
                Description = item.Description,
                OverlapAreaSquareMeters = item.OverlapAreaSquareMeters,
                OverlapPercentage = item.OverlapPercentage,
                SourceName = item.SourceName,
                SourceLayer = item.SourceLayer
            })
            .ToList();

    public static IReadOnlyList<SoilConservationAreaEvidence> ToConservationAreaEvidence(
        IReadOnlyList<SoilConservationCentroidCandidate> matches) =>
        matches
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SoilConservationAreaEvidence
            {
                Id = item.ConservationAreaId,
                Name = item.Name,
                Description = item.Description,
                OverlapAreaSquareMeters = null,
                OverlapPercentage = null,
                SourceName = item.SourceName,
                SourceLayer = item.SourceLayer
            })
            .ToList();

    public static IReadOnlyList<SoilErosionObservationEvidence> ToErosionObservationEvidence(
        IReadOnlyList<SoilErosionObservationCandidate> observations) =>
        observations
            .OrderBy(item => item.DistanceMeters)
            .ThenBy(item => item.ObservationId)
            .Select(item => new SoilErosionObservationEvidence
            {
                Id = item.ObservationId,
                ObservationClass = item.ObservationClass,
                Description = item.Description,
                ErosionRate = item.ErosionRate,
                DistanceMeters = item.DistanceMeters,
                SourceName = item.SourceName,
                SourceLayer = item.SourceLayer
            })
            .ToList();
}
