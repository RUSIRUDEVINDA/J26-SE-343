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
    public void BuildRequestPayload_prefers_gis_derived_natural_water_over_water_supply()
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
