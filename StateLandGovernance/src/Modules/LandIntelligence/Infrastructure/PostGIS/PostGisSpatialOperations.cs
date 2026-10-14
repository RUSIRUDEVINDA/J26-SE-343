using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;

namespace StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

/// <summary>
/// Pure geometry operations used by PostGIS analysis and unit tests.
/// Mirrors PostGIS semantics (ST_Contains, ST_Intersects, ST_Distance) at the NTS layer.
/// </summary>
public static class PostGisSpatialOperations
{
    public static bool IsPointInPolygon(Point point, Geometry polygon) =>
        polygon.Contains(point);

    public static bool GeometriesIntersect(Geometry first, Geometry second) =>
        first.Intersects(second);

    public static bool IsWithinDistanceMeters(
        Point origin,
        Point target,
        double radiusMeters,
        double approximateMetersPerDegree = 111_320d)
    {
        var distanceMeters = CalculateApproximateDistanceMeters(origin, target, approximateMetersPerDegree);
        return distanceMeters <= radiusMeters;
    }

    public static double CalculateApproximateDistanceMeters(
        Point origin,
        Point target,
        double approximateMetersPerDegree = 111_320d)
    {
        var distanceDegrees = origin.Distance(target);
        return distanceDegrees * approximateMetersPerDegree;
    }

    public static double CalculateDistanceMeters(Point origin, Point target) =>
        CalculateApproximateDistanceMeters(origin, target);

    public static double? CalculateAreaSquareMeters(Geometry? geometry, double squareMetersPerSquareDegree = 12_364_000d)
    {
        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        return geometry.Area * squareMetersPerSquareDegree;
    }

    public static bool IsWithinEnvelope(
        Point point,
        double minLongitude,
        double minLatitude,
        double maxLongitude,
        double maxLatitude)
    {
        return point.X >= minLongitude
            && point.X <= maxLongitude
            && point.Y >= minLatitude
            && point.Y <= maxLatitude;
    }

    public static (bool IntersectsBoundary, bool IntersectsCentroid) AnalyzeIntersection(
        Point centroid,
        MultiPolygon? boundary,
        Geometry constraintGeometry)
    {
        var intersectsCentroid = constraintGeometry.Intersects(centroid);
        var intersectsBoundary = boundary is not null && constraintGeometry.Intersects(boundary);

        return (intersectsBoundary, intersectsCentroid);
    }
}
