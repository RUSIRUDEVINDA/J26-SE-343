using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.PostGIS;

public sealed class PostGisGeometryFactoryTests
{
    [Fact]
    public void CreatePolygonFromRing_closes_ring_when_not_closed()
    {
        var ring = new List<CoordinateDto>
        {
            new(0, 0),
            new(0, 1),
            new(1, 1),
            new(1, 0)
        };

        var polygon = PostGisGeometryFactory.CreatePolygonFromRing(ring);

        Assert.NotNull(polygon);
        Assert.Equal(5, polygon!.Coordinates.Length);
        Assert.True(polygon.Coordinates[0].Equals2D(polygon.Coordinates[^1]));
    }

    [Fact]
    public void CreatePoint_uses_longitude_latitude_order()
    {
        var point = PostGisGeometryFactory.CreatePoint(79.8612, 6.9271);

        Assert.Equal(79.8612, point.X);
        Assert.Equal(6.9271, point.Y);
    }
}
