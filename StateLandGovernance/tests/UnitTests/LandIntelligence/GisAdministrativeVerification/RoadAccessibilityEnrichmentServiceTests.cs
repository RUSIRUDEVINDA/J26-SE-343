using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

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
        Assert.Contains("<->", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("gis_water_features", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("InfrastructureFeatures", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("5000", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("maxRoadDistance", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Railway", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildNearestRoadSql_filters_source_layer_when_configured()
    {
        var sql = RoadAccessibilityEnrichmentService.BuildNearestRoadSql(
            GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);

        Assert.Contains("@sourceLayer", sql, StringComparison.Ordinal);
        Assert.Contains("SourceLayer", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OsmMotorRoadAttributeEncoding_round_trips_highway_and_maps_expressway()
    {
        var encoded = OsmMotorRoadAttributeEncoding.EncodeName("motorway", "Southern Link");
        Assert.Equal("motorway", OsmMotorRoadAttributeEncoding.TryDecodeHighway(encoded));
        Assert.Equal("Southern Link", OsmMotorRoadAttributeEncoding.TryDecodeDisplayName(encoded));
        Assert.Equal(
            StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums.GisRoadType.Expressway,
            OsmMotorRoadAttributeEncoding.MapHighwayToRoadType("motorway"));
    }
}
