using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

public static class PostGisGeometryFactory
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    public static Point CreatePoint(double longitude, double latitude) =>
        Factory.CreatePoint(new Coordinate(longitude, latitude));

    public static Polygon? CreatePolygonFromRing(IReadOnlyList<CoordinateDto>? ring)
    {
        if (ring is null || ring.Count < 3)
        {
            return null;
        }

        var coordinates = ring
            .Select(coordinate => new Coordinate(coordinate.Longitude, coordinate.Latitude))
            .ToList();

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
        }

        return Factory.CreatePolygon(coordinates.ToArray());
    }

    public static Geometry? ResolvePolygonGeometry(
        IReadOnlyList<CoordinateDto>? polygonRing,
        MultiPolygon? constraintGeometry)
    {
        if (constraintGeometry is not null)
        {
            return constraintGeometry;
        }

        return CreatePolygonFromRing(polygonRing);
    }

    public static Geometry ResolveParcelGeometry(Point centroid, MultiPolygon? boundary) =>
        (Geometry?)boundary ?? centroid;
}
