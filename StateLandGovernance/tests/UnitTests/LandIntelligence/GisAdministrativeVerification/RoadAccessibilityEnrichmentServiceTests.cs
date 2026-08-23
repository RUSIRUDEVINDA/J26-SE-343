using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class RoadAccessibilityEnrichmentServiceTests
{
    [Fact]
    public void BuildNearestRoadSql_queries_only_gis_roads_with_geography_distance()
    {
        var sql = RoadAccessibilityEnrichmentService.BuildNearestRoadSql();

        Assert.Contains("gis_roads", sql, StringComparison.Ordinal);
        Assert.Contains("ST_Distance", sql, StringComparison.Ordinal);
        Assert.Contains("::geography", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_water_features", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("InfrastructureFeatures", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("5000", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("maxRoadDistance", sql, StringComparison.OrdinalIgnoreCase);
    }
}
