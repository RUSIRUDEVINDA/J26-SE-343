using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Database-driven spatial analysis for land parcels using PostgreSQL/PostGIS.
/// </summary>
public interface ISpatialAnalysisService
{
    Task<PointInPolygonResultDto> IsPointInPolygonAsync(
        PointInPolygonRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParcelIntersectionResultDto>> FindParcelsIntersectingConstraintAsync(
        Guid spatialConstraintId,
        CancellationToken cancellationToken = default);

    Task<GeometryIntersectionResultDto> CheckGeometriesIntersectAsync(
        GeometryIntersectionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProximityResultDto>> FindParcelsNearPointAsync(
        ProximitySearchRequest request,
        CancellationToken cancellationToken = default);

    Task<DistanceResultDto> CalculateDistanceBetweenParcelAndInfrastructureAsync(
        Guid landParcelId,
        Guid infrastructureFeatureId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpatialFilterResultDto>> FilterParcelsAsync(
        SpatialFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<AreaCalculationResultDto> CalculateParcelAreaAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpatialConstraintDetectionDto>> DetectSpatialConstraintsForParcelAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpatialConstraintDto>> AnalyzeConstraintsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
