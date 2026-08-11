using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record CoordinateDto(double Latitude, double Longitude);

public sealed record PointInPolygonRequest
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public Guid? SpatialConstraintId { get; init; }
    public IReadOnlyList<CoordinateDto>? PolygonRing { get; init; }
}

public sealed record PointInPolygonResultDto(
    double Latitude,
    double Longitude,
    bool IsInside,
    Guid? SpatialConstraintId,
    string GeometrySource);

public sealed record GeometryIntersectionRequest
{
    public required Guid LandParcelId { get; init; }
    public Guid? SpatialConstraintId { get; init; }
}

public sealed record GeometryIntersectionResultDto(
    Guid LandParcelId,
    Guid? SpatialConstraintId,
    bool Intersects,
    string IntersectionType);

public sealed record ParcelIntersectionResultDto(
    Guid LandParcelId,
    string CadastralNumber,
    Guid SpatialConstraintId,
    bool IntersectsBoundary,
    bool IntersectsCentroid);

public sealed record ProximitySearchRequest
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double RadiusMeters { get; init; }
    public int MaxResults { get; init; } = 50;
}

public sealed record ProximityResultDto(
    Guid LandParcelId,
    string CadastralNumber,
    double CentroidLatitude,
    double CentroidLongitude,
    double DistanceMeters);

public sealed record DistanceResultDto(
    Guid LandParcelId,
    Guid InfrastructureFeatureId,
    string InfrastructureName,
    InfrastructureFeatureType InfrastructureType,
    double DistanceMeters,
    bool WithinStoredDistance);

public sealed record SpatialFilterRequest
{
    public double? MinLatitude { get; init; }
    public double? MaxLatitude { get; init; }
    public double? MinLongitude { get; init; }
    public double? MaxLongitude { get; init; }
    public Guid? IntersectingConstraintId { get; init; }
    public double? NearLatitude { get; init; }
    public double? NearLongitude { get; init; }
    public double? NearRadiusMeters { get; init; }
    public int MaxResults { get; init; } = 100;
}

public sealed record SpatialFilterResultDto(
    Guid LandParcelId,
    string CadastralNumber,
    string Province,
    string District,
    double CentroidLatitude,
    double CentroidLongitude,
    double? DistanceMeters);

public sealed record AreaCalculationResultDto(
    Guid LandParcelId,
    double? CalculatedAreaSquareMeters,
    decimal? StoredAreaValue,
    AreaUnit? StoredAreaUnit,
    string AreaSource);

public sealed record SpatialConstraintDetectionDto(
    Guid ConstraintId,
    Guid LandParcelId,
    SpatialConstraintType Type,
    string Description,
    RestrictionSeverity Severity,
    string DetectionReason,
    bool IntersectsBoundary,
    bool IntersectsCentroid);
