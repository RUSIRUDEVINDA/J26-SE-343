using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

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

    public static MultiPolygon? ToMultiPolygon(GeoBoundary? boundary)
    {
        if (boundary is null)
        {
            return null;
        }

        var polygon = CreatePolygonFromRing(
            boundary.ExteriorRing
                .Select(coordinate => new CoordinateDto(coordinate.Latitude, coordinate.Longitude))
                .ToList());

        return polygon is null
            ? null
            : Factory.CreateMultiPolygon(new[] { polygon });
    }

    public static GeoBoundary? ToGeoBoundary(MultiPolygon? multiPolygon)
    {
        if (multiPolygon is null || multiPolygon.IsEmpty)
        {
            return null;
        }

        var polygon = multiPolygon.NumGeometries > 0
            ? multiPolygon.GetGeometryN(0) as Polygon
            : null;

        if (polygon is null || polygon.IsEmpty)
        {
            return null;
        }

        var coordinates = polygon.ExteriorRing.Coordinates
            .Where(coordinate => !double.IsNaN(coordinate.X) && !double.IsNaN(coordinate.Y))
            .Select(coordinate => new GeoCoordinate(coordinate.Y, coordinate.X))
            .ToList();

        if (coordinates.Count > 1
            && coordinates[0].Latitude.Equals(coordinates[^1].Latitude)
            && coordinates[0].Longitude.Equals(coordinates[^1].Longitude))
        {
            coordinates.RemoveAt(coordinates.Count - 1);
        }

        return coordinates.Count < 3 ? null : new GeoBoundary(coordinates);
    }

    public static Point? ToPoint(GeoCoordinate? coordinate) =>
        coordinate is null ? null : CreatePoint(coordinate.Longitude, coordinate.Latitude);

    public static GeoCoordinate? ToGeoCoordinate(Point? point) =>
        point is null || point.IsEmpty ? null : new GeoCoordinate(point.Y, point.X);

    public static Geometry ResolveParcelGeometry(Point centroid, MultiPolygon? boundary) =>
        (Geometry?)boundary ?? centroid;
}
