using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class SoilGroupEnrichmentServiceTests
{
    [Fact]
    public void BuildBoundaryOverlapSql_uses_geography_area_intersection_on_gis_soil_groups()
    {
        var sql = SoilGroupEnrichmentService.BuildBoundaryOverlapSql();

        Assert.Contains("gis_soil_groups", sql, StringComparison.Ordinal);
        Assert.Contains("land_parcels", sql, StringComparison.Ordinal);
        Assert.Contains("@parcelId", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Intersection", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Area", sql, StringComparison.Ordinal);
        Assert.Contains("::geography", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_roads", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCentroidSoilGroupSql_uses_point_in_polygon_on_gis_soil_groups()
    {
        var sql = SoilGroupEnrichmentService.BuildCentroidSoilGroupSql();

        Assert.Contains("gis_soil_groups", sql, StringComparison.Ordinal);
        Assert.Contains("land_parcels", sql, StringComparison.Ordinal);
        Assert.Contains("@parcelId", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Covers", sql, StringComparison.Ordinal);
    }
}
