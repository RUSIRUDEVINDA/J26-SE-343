using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class WaterProximityEnrichmentServiceTests
{
    [Fact]
    public void BuildNearestWaterFeatureSql_queries_only_canals_and_lakes_with_geography_distance()
    {
        var sql = WaterProximityEnrichmentService.BuildNearestWaterFeatureSql();

        Assert.Contains("gis_water_features", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Distance", sql, StringComparison.Ordinal);
        Assert.Contains("::geography", sql, StringComparison.Ordinal);
        Assert.Contains("FeatureType", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_roads", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_soil_groups", sql, StringComparison.Ordinal);
    }
}
