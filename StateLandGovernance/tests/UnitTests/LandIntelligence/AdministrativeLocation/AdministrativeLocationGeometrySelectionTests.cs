using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class AdministrativeLocationGeometrySelectionTests
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    [Fact]
    public void SelectParcelGeometry_prefers_boundary_when_available()
    {
        var boundary = GeometryFactory.CreateMultiPolygon([
            GeometryFactory.CreatePolygon([
                new Coordinate(80.1, 6.1),
                new Coordinate(80.2, 6.1),
                new Coordinate(80.2, 6.2),
                new Coordinate(80.1, 6.2),
                new Coordinate(80.1, 6.1)
            ])
        ]);
        var centroid = GeometryFactory.CreatePoint(new Coordinate(80.15, 6.15));

        var selection = AdministrativeLocationVerificationService.SelectParcelGeometry(boundary, centroid);

        Assert.NotNull(selection);
        Assert.Equal(AdministrativeLocationGeometryBasis.Boundary, selection.Value.Basis);
        Assert.Equal(boundary, selection.Value.Geometry);
    }

    [Fact]
    public void SelectParcelGeometry_falls_back_to_centroid_when_boundary_is_unavailable()
    {
        var centroid = GeometryFactory.CreatePoint(new Coordinate(80.15, 6.15));

        var selection = AdministrativeLocationVerificationService.SelectParcelGeometry(null, centroid);

        Assert.NotNull(selection);
        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, selection.Value.Basis);
        Assert.Equal(centroid, selection.Value.Geometry);
    }

    [Fact]
    public void SelectParcelGeometry_returns_null_when_boundary_and_centroid_are_unavailable()
    {
        var emptyCentroid = GeometryFactory.CreatePoint();

        var selection = AdministrativeLocationVerificationService.SelectParcelGeometry(null, emptyCentroid);

        Assert.Null(selection);
    }
}
