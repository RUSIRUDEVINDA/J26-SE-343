using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.PostGIS;

public sealed class PostGisSpatialOperationsTests
{
    [Fact]
    public void IsPointInPolygon_returns_true_for_point_inside_square()
    {
        var polygon = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(6.90, 79.84),
            new(6.90, 79.88),
            new(6.95, 79.88),
            new(6.95, 79.84)
        ])!;

        var insidePoint = PostGisGeometryFactory.CreatePoint(79.86, 6.9271);

        Assert.True(PostGisSpatialOperations.IsPointInPolygon(insidePoint, polygon));
    }

    [Fact]
    public void IsPointInPolygon_returns_false_for_point_outside_square()
    {
        var polygon = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(6.90, 79.84),
            new(6.90, 79.88),
            new(6.95, 79.88),
            new(6.95, 79.84)
        ])!;

        var outsidePoint = PostGisGeometryFactory.CreatePoint(80.10, 7.10);

        Assert.False(PostGisSpatialOperations.IsPointInPolygon(outsidePoint, polygon));
    }

    [Fact]
    public void GeometriesIntersect_returns_true_for_overlapping_polygons()
    {
        var first = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(6.90, 79.84),
            new(6.90, 79.88),
            new(6.95, 79.88),
            new(6.95, 79.84)
        ])!;

        var second = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(6.93, 79.86),
            new(6.93, 79.90),
            new(6.98, 79.90),
            new(6.98, 79.86)
        ])!;

        Assert.True(PostGisSpatialOperations.GeometriesIntersect(first, second));
    }

    [Fact]
    public void IsWithinDistanceMeters_returns_true_for_nearby_points()
    {
        var origin = PostGisGeometryFactory.CreatePoint(79.8612, 6.9271);
        var nearby = PostGisGeometryFactory.CreatePoint(79.8613, 6.9272);

        Assert.True(PostGisSpatialOperations.IsWithinDistanceMeters(origin, nearby, 500));
    }

    [Fact]
    public void IsWithinDistanceMeters_returns_false_for_far_points()
    {
        var origin = PostGisGeometryFactory.CreatePoint(79.8612, 6.9271);
        var far = PostGisGeometryFactory.CreatePoint(80.50, 7.50);

        Assert.False(PostGisSpatialOperations.IsWithinDistanceMeters(origin, far, 500));
    }

    [Fact]
    public void IsWithinEnvelope_returns_true_for_point_inside_bounds()
    {
        var point = PostGisGeometryFactory.CreatePoint(79.86, 6.92);

        Assert.True(PostGisSpatialOperations.IsWithinEnvelope(point, 79.84, 6.90, 79.88, 6.95));
    }

    [Fact]
    public void AnalyzeIntersection_detects_centroid_intersection()
    {
        var centroid = PostGisGeometryFactory.CreatePoint(79.86, 6.9271);
        var constraint = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(6.90, 79.84),
            new(6.90, 79.88),
            new(6.95, 79.88),
            new(6.95, 79.84)
        ])!;

        var result = PostGisSpatialOperations.AnalyzeIntersection(centroid, boundary: null, constraint);

        Assert.False(result.IntersectsBoundary);
        Assert.True(result.IntersectsCentroid);
    }

    [Fact]
    public void CalculateAreaSquareMeters_returns_positive_value_for_polygon()
    {
        var polygon = PostGisGeometryFactory.CreatePolygonFromRing(
        [
            new(0, 0),
            new(0, 1),
            new(1, 1),
            new(1, 0)
        ])!;

        var area = PostGisSpatialOperations.CalculateAreaSquareMeters(polygon);

        Assert.NotNull(area);
        Assert.True(area > 0);
    }
}
