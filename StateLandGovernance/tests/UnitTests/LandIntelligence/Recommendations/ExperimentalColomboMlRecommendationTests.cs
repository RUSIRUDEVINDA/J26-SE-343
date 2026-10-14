using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class ExperimentalColomboMlRecommendationTests
{
    [Fact]
    public async Task Default_disabled_keeps_production_ml_path_and_skips_experimental()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var trackingMl = new TrackingMlSuitabilityClient(
            new MlSuitabilityPrediction("Suitable", new Dictionary<string, decimal>
            {
                ["Suitable"] = 0.9m
            }));
        var experimental = new StubExperimentalColomboMlEvidenceService(
            new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = true,
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        "ExperimentalColomboOsmRf",
                        "should not appear",
                        "ExperimentalColomboMlPrediction")
                ]
            });

        var engine = RecommendationEngineTestFactory.CreateWithExperimental(
            experimental,
            new ExperimentalColomboMlOptions { Enabled = false },
            trackingMl,
            parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 5
        });

        Assert.True(trackingMl.WasCalled);
        Assert.Equal(0, experimental.CallCount);
        Assert.DoesNotContain(
            response.Recommendations[0].Evidence,
            e => e.Source == "ExperimentalColomboOsmRf");
        Assert.Contains(
            response.Recommendations[0].Evidence,
            e => e.Source == "RandomForestSuitabilityModel");
    }

    [Fact]
    public async Task Enabled_with_suppress_skips_production_ml_and_adds_experimental_evidence()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var trackingMl = new TrackingMlSuitabilityClient(
            new MlSuitabilityPrediction("Suitable", new Dictionary<string, decimal>
            {
                ["Suitable"] = 0.9m
            }));
        var experimental = new StubExperimentalColomboMlEvidenceService(
            new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = true,
                Prediction = new ExperimentalColomboMlPrediction
                {
                    PredictedLabel = "Suitable",
                    Probabilities = new Dictionary<string, decimal> { ["Suitable"] = 0.8m },
                    CandidateId = "colombo_osm_backend_compatible",
                    ModelDir = "output_colombo_osm_backend_compatible",
                    Limitations = ["Supplementary only."]
                },
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        "ExperimentalColomboOsmRf",
                        "Experimental candidate 'colombo_osm_backend_compatible' predicts 'Suitable'.",
                        "ExperimentalColomboMlPrediction")
                ]
            });

        var engine = RecommendationEngineTestFactory.CreateWithExperimental(
            experimental,
            new ExperimentalColomboMlOptions
            {
                Enabled = true,
                SuppressProductionMlWhenEnabled = true
            },
            trackingMl,
            parcel);

        var baseline = await RecommendationEngineTestFactory
            .Create(parcel)
            .RecommendAsync(new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                MaxResults = 5
            });

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 5
        });

        Assert.False(trackingMl.WasCalled);
        Assert.Equal(1, experimental.CallCount);
        Assert.Equal(
            baseline.Recommendations[0].SuitabilityScore,
            response.Recommendations[0].SuitabilityScore);
        Assert.Contains(
            response.Recommendations[0].Evidence,
            e => e.Source == "ExperimentalColomboOsmRf"
                 && e.RelatedCriterionName == "ExperimentalColomboMlPrediction");
        Assert.DoesNotContain(
            response.Recommendations[0].Evidence,
            e => e.Source == "RandomForestSuitabilityModel");
    }

    [Fact]
    public async Task Hard_rejection_skips_both_ml_paths()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateRestrictedParcel();
        var trackingMl = new TrackingMlSuitabilityClient(
            new MlSuitabilityPrediction("Unsuitable", new Dictionary<string, decimal>
            {
                ["Unsuitable"] = 0.9m
            }));
        var experimental = new StubExperimentalColomboMlEvidenceService(
            new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = true,
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        "ExperimentalColomboOsmRf",
                        "should not appear on hard reject",
                        "ExperimentalColomboMlPrediction")
                ]
            });

        var engine = RecommendationEngineTestFactory.CreateWithExperimental(
            experimental,
            new ExperimentalColomboMlOptions
            {
                Enabled = true,
                SuppressProductionMlWhenEnabled = true
            },
            trackingMl,
            parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            RequiredAreaHectares = 5m,
            TargetParcelId = parcel.Id,
            MaxResults = 1
        });

        Assert.True(response.Recommendations[0].HardConstraintRejected);
        Assert.False(trackingMl.WasCalled);
        Assert.Equal(0, experimental.CallCount);
        Assert.Contains(
            response.Recommendations[0].Evidence,
            e => e.RelatedCriterionName == "HardConstraintRejection");
        Assert.DoesNotContain(
            response.Recommendations[0].Evidence,
            e => e.Source == "ExperimentalColomboOsmRf");
    }

    [Fact]
    public async Task Abstention_evidence_does_not_change_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var experimental = new StubExperimentalColomboMlEvidenceService(
            new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = false,
                AbstentionReason =
                    "Experimental prediction abstained: conservation assessment unavailable",
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        "ExperimentalColomboOsmRf",
                        "Experimental Colombo ML abstained. Conservation nulls were not coerced.",
                        "ExperimentalColomboMlAbstention")
                ]
            });

        var engine = RecommendationEngineTestFactory.CreateWithExperimental(
            experimental,
            new ExperimentalColomboMlOptions { Enabled = true },
            parcels: parcel);

        var baseline = await RecommendationEngineTestFactory
            .Create(parcel)
            .RecommendAsync(new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                MaxResults = 5
            });

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 5
        });

        Assert.Equal(
            baseline.Recommendations[0].SuitabilityScore,
            response.Recommendations[0].SuitabilityScore);
        Assert.Contains(
            response.Recommendations[0].Evidence,
            e => e.RelatedCriterionName == "ExperimentalColomboMlAbstention");
    }

    [Fact]
    public async Task Unavailable_experimental_service_preserves_rule_based_response()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var experimental = new StubExperimentalColomboMlEvidenceService(
            new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = true,
                FailureReason = "Experimental Colombo ML service unavailable",
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        "ExperimentalColomboOsmRf",
                        "Experimental Colombo ML service unavailable, timed out, or schema mismatch.",
                        "ExperimentalColomboMlUnavailable")
                ]
            });

        var engine = RecommendationEngineTestFactory.CreateWithExperimental(
            experimental,
            new ExperimentalColomboMlOptions { Enabled = true },
            parcels: parcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 5
        });

        Assert.False(response.Recommendations[0].HardConstraintRejected);
        Assert.True(response.Recommendations[0].SuitabilityScore > 0);
        Assert.Contains(
            response.Recommendations[0].Evidence,
            e => e.RelatedCriterionName == "ExperimentalColomboMlUnavailable");
        Assert.DoesNotContain(
            response.Recommendations[0].Evidence,
            e => e.RelatedCriterionName == "ExperimentalColomboMlPrediction"
                 && e.Description.Contains("predicts", StringComparison.OrdinalIgnoreCase));
    }
}
