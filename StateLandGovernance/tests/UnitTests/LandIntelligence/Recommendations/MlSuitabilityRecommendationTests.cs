using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class MlSuitabilityRecommendationTests
{
    [Fact]
    public void BuildRequestPayload_uses_gis_derived_natural_water_and_ignores_water_supply()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedNaturalWaterOnly(150m);
        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.WaterSupply,
            "[SYNTHETIC] Utility supply",
            900m));

        var payload = HttpMlSuitabilityClient.BuildRequestPayload(parcel, LandUseType.Agricultural);

        Assert.Equal(150d, payload.DistanceToWaterM);
    }

    [Fact]
    public void BuildRequestPayload_does_not_substitute_official_soil_when_gis_soil_missing()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();

        var payload = HttpMlSuitabilityClient.BuildRequestPayload(parcel, LandUseType.Agricultural);

        Assert.Equal("Unknown", payload.DerivedSoilGroup);
        Assert.Null(payload.SoilOverlapPercentage);
        Assert.Equal("Unavailable", payload.GisEnrichmentStatus);
    }

    [Fact]
    public void BuildRequestPayload_nulls_gis_distances_when_enrichment_unavailable()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithUnavailableGisEnrichment();

        var payload = HttpMlSuitabilityClient.BuildRequestPayload(parcel, LandUseType.Agricultural);

        Assert.Null(payload.DistanceToRoadM);
        Assert.Null(payload.DistanceToWaterM);
        Assert.Equal("Unknown", payload.DerivedSoilGroup);
        Assert.Null(payload.SoilOverlapPercentage);
        Assert.Equal("Unknown", payload.TerrainDescription);
    }

    [Fact]
    public void BuildRequestPayload_ignores_non_gis_road_distance()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateModerateParcel();

        var payload = HttpMlSuitabilityClient.BuildRequestPayload(parcel, LandUseType.Agricultural);

        Assert.Null(payload.DistanceToRoadM);
    }

    [Fact]
    public void BuildRequestPayload_maps_gis_derived_soil_when_present()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedSoil(
            officialSoilType: "Loam",
            gisSoilGroupName: "Red Yellow Latosols");

        var payload = HttpMlSuitabilityClient.BuildRequestPayload(parcel, LandUseType.Agricultural);

        Assert.Equal("Red Yellow Latosols", payload.DerivedSoilGroup);
        Assert.Equal(92.5d, payload.SoilOverlapPercentage);
    }

    [Fact]
    public async Task PredictAsync_returns_null_when_ml_service_is_unreachable()
    {
        var client = new HttpMlSuitabilityClient(
            new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1"), Timeout = TimeSpan.FromMilliseconds(100) },
            NullLogger<HttpMlSuitabilityClient>.Instance);

        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();

        var prediction = await client.PredictAsync(parcel, LandUseType.Agricultural);

        Assert.Null(prediction);
    }

    [Fact]
    public async Task RecommendAsync_appends_ml_evidence_without_changing_suitability_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var mlPrediction = new MlSuitabilityPrediction(
            "Suitable",
            new Dictionary<string, decimal> { ["Suitable"] = 0.82m, ["Unsuitable"] = 0.18m });

        var withoutMl = RecommendationEngineTestFactory.CreateWithMlClient(new NullMlSuitabilityClient(), parcel);
        var withMl = RecommendationEngineTestFactory.CreateWithMlClient(
            new FixedMlSuitabilityClient(mlPrediction),
            parcel);

        var request = new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            RequiredAreaHectares = 5m,
            TargetParcelId = parcel.Id,
            MaxResults = 1
        };

        var baseline = await withoutMl.RecommendAsync(request);
        var enriched = await withMl.RecommendAsync(request);

        var baselineRecommendation = Assert.Single(baseline.Recommendations);
        var enrichedRecommendation = Assert.Single(enriched.Recommendations);

        Assert.Equal(baselineRecommendation.SuitabilityScore, enrichedRecommendation.SuitabilityScore);
        Assert.Contains(
            enrichedRecommendation.Evidence,
            evidence => evidence.RelatedCriterionName == "MlSuitabilityPrediction");
        Assert.Contains(
            enrichedRecommendation.Evidence,
            evidence => evidence.Description.Contains("RandomForest", StringComparison.OrdinalIgnoreCase)
                || evidence.Source == "RandomForestSuitabilityModel");
        Assert.DoesNotContain(
            baselineRecommendation.Evidence,
            evidence => evidence.RelatedCriterionName == "MlSuitabilityPrediction");
    }

    [Fact]
    public async Task RecommendAsync_skips_ml_when_hard_constraint_rejected()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateRestrictedParcel();
        var trackingMlClient = new TrackingMlSuitabilityClient(
            new MlSuitabilityPrediction(
                "Suitable",
                new Dictionary<string, decimal> { ["Suitable"] = 0.95m }));
        var engine = RecommendationEngineTestFactory.CreateWithMlClient(trackingMlClient, parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            RequiredAreaHectares = 5m,
            TargetParcelId = parcel.Id,
            MaxResults = 1
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.True(recommendation.HardConstraintRejected);
        Assert.Equal(0m, recommendation.SuitabilityScore);
        Assert.False(trackingMlClient.WasCalled);
        Assert.Contains(recommendation.Evidence, e => e.RelatedCriterionName == "HardConstraintRejection");
        Assert.DoesNotContain(
            recommendation.Evidence,
            e => e.RelatedCriterionName == "MlSuitabilityPrediction");
    }

    [Fact]
    public async Task RecommendAsync_continues_when_ml_client_returns_null()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.CreateWithMlClient(new NullMlSuitabilityClient(), parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            RequiredAreaHectares = 5m,
            TargetParcelId = parcel.Id,
            MaxResults = 1
        });

        Assert.Single(response.Recommendations);
    }
}
