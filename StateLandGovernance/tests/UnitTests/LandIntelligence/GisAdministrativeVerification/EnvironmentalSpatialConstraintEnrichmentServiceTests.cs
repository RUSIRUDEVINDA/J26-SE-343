using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class EnvironmentalSpatialConstraintEnrichmentServiceTests
{
    [Fact]
    public void BuildBoundaryConservationOverlapSql_uses_geography_area_intersection_on_gis_soil_conservation_areas()
    {
        var sql = EnvironmentalSpatialConstraintEnrichmentService.BuildBoundaryConservationOverlapSql();

        Assert.Contains("gis_soil_conservation_areas", sql, StringComparison.Ordinal);
        Assert.Contains("land_parcels", sql, StringComparison.Ordinal);
        Assert.Contains("@parcelId", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Intersection", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Area", sql, StringComparison.Ordinal);
        Assert.Contains("::geography", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_soil_groups", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCentroidConservationAreasSql_uses_point_in_polygon_on_gis_soil_conservation_areas()
    {
        var sql = EnvironmentalSpatialConstraintEnrichmentService.BuildCentroidConservationAreasSql();

        Assert.Contains("gis_soil_conservation_areas", sql, StringComparison.Ordinal);
        Assert.Contains("land_parcels", sql, StringComparison.Ordinal);
        Assert.Contains("@parcelId", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Covers", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildErosionObservationsInPilotSql_checks_pilot_district_intersection()
    {
        var sql = EnvironmentalSpatialConstraintEnrichmentService.BuildErosionObservationsInPilotSql();

        Assert.Contains("gis_soil_erosion_observations", sql, StringComparison.Ordinal);
        Assert.Contains("gis_administrative_boundaries", sql, StringComparison.Ordinal);
        Assert.Contains("@districtName", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Intersects", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBoundaryErosionObservationsSql_uses_intersection_and_dwithin()
    {
        var sql = EnvironmentalSpatialConstraintEnrichmentService.BuildBoundaryErosionObservationsSql();

        Assert.Contains("gis_soil_erosion_observations", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Intersects", sql, StringComparison.Ordinal);
        Assert.Contains("ST_DWithin", sql, StringComparison.Ordinal);
        Assert.Contains("@proximityRadiusMeters", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCentroidErosionObservationsSql_uses_dwithin_from_centroid()
    {
        var sql = EnvironmentalSpatialConstraintEnrichmentService.BuildCentroidErosionObservationsSql();

        Assert.Contains("gis_soil_erosion_observations", sql, StringComparison.Ordinal);
        Assert.Contains("ST_DWithin", sql, StringComparison.Ordinal);
        Assert.Contains("@proximityRadiusMeters", sql, StringComparison.Ordinal);
    }
}
