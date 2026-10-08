using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class GeoJsonGeometryMapper
{
    public static GeoBoundary? ToGeoBoundary(GeoJsonPolygonDto? geometry)
    {
        if (geometry?.Coordinates is null || geometry.Coordinates.Count == 0)
        {
            return null;
        }

        var ring = ResolveExteriorRing(geometry);
        if (ring is null || ring.Count < 3)
        {
            return null;
        }

        var coordinates = ring
            .Select(position => ToGeoCoordinate(position))
            .Where(coordinate => coordinate is not null)
            .Select(coordinate => coordinate!)
            .ToList();

        return coordinates.Count < 3 ? null : new GeoBoundary(coordinates);
    }

    public static GeoJsonPolygonDto? ToGeoJson(GeoBoundary? boundary) =>
        boundary is null
            ? null
            : new GeoJsonPolygonDto
            {
                Type = "Polygon",
                Coordinates =
                [
                    boundary.ExteriorRing
                        .Select(coordinate => (IReadOnlyList<double>)[coordinate.Longitude, coordinate.Latitude])
                        .ToList()
                ]
            };

    private static IReadOnlyList<IReadOnlyList<double>>? ResolveExteriorRing(GeoJsonPolygonDto geometry)
    {
        if (geometry.Coordinates is null || geometry.Coordinates.Count == 0)
        {
            return null;
        }

        // API accepts GeoJSON Polygon rings at the top level (Position[][]).
        return geometry.Coordinates[0];
    }

    private static GeoCoordinate? ToGeoCoordinate(IReadOnlyList<double> position)
    {
        if (position.Count < 2)
        {
            return null;
        }

        return new GeoCoordinate(position[1], position[0]);
    }
}
