using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisReferenceData;

public sealed class GisGeometryNormalizerTests
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    [Fact]
    public void ToMultiPolygon_converts_polygon_to_multi_polygon_with_srid_4326()
    {
        var polygon = Factory.CreatePolygon(new Coordinate[]
        {
            new(80.0, 6.0),
            new(80.1, 6.0),
            new(80.1, 6.1),
            new(80.0, 6.1),
            new(80.0, 6.0)
        });

        var result = GisGeometryNormalizer.ToMultiPolygon(polygon);

        Assert.NotNull(result);
        Assert.Equal(4326, result!.SRID);
        Assert.Equal("MultiPolygon", result.GeometryType);
        Assert.Equal(1, result.NumGeometries);
    }

    [Fact]
    public void ToMultiLineString_converts_line_string_to_multi_line_string_with_srid_4326()
    {
        var line = Factory.CreateLineString(new Coordinate[]
        {
            new(80.0, 6.0),
            new(80.2, 6.2)
        });

        var result = GisGeometryNormalizer.ToMultiLineString(line);

        Assert.NotNull(result);
        Assert.Equal(4326, result!.SRID);
        Assert.Equal("MultiLineString", result.GeometryType);
    }

    [Fact]
    public void IsSpatiallyRelevant_includes_features_inside_crossing_and_on_boundary()
    {
        var pilot = Factory.CreatePolygon(new Coordinate[]
        {
            new(80.0, 6.0),
            new(80.2, 6.0),
            new(80.2, 6.2),
            new(80.0, 6.2),
            new(80.0, 6.0)
        });

        var inside = Factory.CreatePoint(new Coordinate(80.1, 6.1));
        var outside = Factory.CreatePoint(new Coordinate(81.0, 7.0));
        var crossing = Factory.CreateLineString(new Coordinate[]
        {
            new(79.9, 6.1),
            new(80.3, 6.1)
        });

        Assert.True(GisGeometryNormalizer.IsSpatiallyRelevant(inside, pilot));
        Assert.False(GisGeometryNormalizer.IsSpatiallyRelevant(outside, pilot));
        Assert.True(GisGeometryNormalizer.IsSpatiallyRelevant(crossing, pilot));
    }

    [Fact]
    public void PrepareGeometry_rejects_empty_geometry()
    {
        var empty = Factory.CreatePoint();

        Assert.Null(GisGeometryNormalizer.PrepareGeometry(empty));
    }
}
