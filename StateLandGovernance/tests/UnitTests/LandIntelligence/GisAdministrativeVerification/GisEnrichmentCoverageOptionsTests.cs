using StateLandGovernance.LandIntelligence.Application.Configuration;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class GisEnrichmentCoverageOptionsTests
{
    [Fact]
    public void ResolveRoadSourceLayerForDistrict_keeps_hambantota_expressways_when_colombo_uses_osm()
    {
        var options = new GisEnrichmentCoverageOptions
        {
            SupportedDistrictNames = ["Hambantota", "Colombo"],
            RoadSourceLayer = GisEnrichmentCoverageDefaults.ExpresswaysLayer,
            DistrictRoadSourceLayers =
            [
                new DistrictRoadSourceLayerOptions
                {
                    DistrictName = "Hambantota",
                    RoadSourceLayer = GisEnrichmentCoverageDefaults.ExpresswaysLayer
                },
                new DistrictRoadSourceLayerOptions
                {
                    DistrictName = "Colombo",
                    RoadSourceLayer = GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer
                }
            ]
        };

        Assert.Equal(
            GisEnrichmentCoverageDefaults.ExpresswaysLayer,
            options.ResolveRoadSourceLayerForDistrict("Hambantota"));
        Assert.Equal(
            GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
            options.ResolveRoadSourceLayerForDistrict("Colombo"));
        Assert.Equal(
            GisEnrichmentCoverageDefaults.ExpresswaysLayer,
            options.ResolveRoadSourceLayerForDistrict("Kandy"));
    }
}
