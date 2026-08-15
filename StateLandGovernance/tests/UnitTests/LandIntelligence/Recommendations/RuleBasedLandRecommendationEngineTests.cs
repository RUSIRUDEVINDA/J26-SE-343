using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class RuleBasedLandRecommendationEngineTests
{
    [Fact]
    public async Task RecommendAsync_returns_explainable_result_for_suitable_parcel()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Equal(parcel.Id, recommendation.ParcelId);
        Assert.True(recommendation.SuitabilityScore >= 70m);
        Assert.Equal(1, recommendation.Rank);
        Assert.NotEmpty(recommendation.MatchingCriteria);
        Assert.NotEmpty(recommendation.Evidence);
        Assert.Contains("suitability score", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Positive factors", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_marks_restricted_parcel_with_lower_score_and_restrictions()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateRestrictedParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            RequiredLandCategory = LandCategoryType.ReservedLand,
            RequiredLandUse = LandUseType.Conservation,
            RequiredPurpose = LandUseType.Conservation
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.True(recommendation.SuitabilityScore < 70m);
        Assert.NotEmpty(recommendation.FailedCriteria);
        Assert.NotEmpty(recommendation.Restrictions);
        Assert.Contains(recommendation.Restrictions, r => r.Severity >= RestrictionSeverity.High);
        Assert.Contains("Restrictions", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_ranks_multiple_candidates_by_score()
    {
        var suitable = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-RANK-001");
        var moderate = SyntheticRecommendationParcelFactory.CreateModerateParcel("SYNTH-RANK-002");
        var restricted = SyntheticRecommendationParcelFactory.CreateRestrictedParcel("SYNTH-RANK-003");
        var engine = RecommendationEngineTestFactory.Create(suitable, moderate, restricted);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 3
        });

        Assert.Equal(3, response.Recommendations.Count);
        Assert.Equal(3, response.CandidateCount);
        Assert.Equal(1, response.Recommendations[0].Rank);
        Assert.Equal(2, response.Recommendations[1].Rank);
        Assert.Equal(3, response.Recommendations[2].Rank);
        Assert.True(response.Recommendations[0].SuitabilityScore >= response.Recommendations[1].SuitabilityScore);
        Assert.True(response.Recommendations[1].SuitabilityScore >= response.Recommendations[2].SuitabilityScore);
    }

    [Fact]
    public async Task RecommendAsync_includes_failed_criteria_for_mismatched_category()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            RequiredLandCategory = LandCategoryType.CrownLand
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Contains(recommendation.FailedCriteria, c => c.Key == "land-category");
        Assert.Contains("Negative factors", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsync_generates_explanation_with_positive_and_negative_factors()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateModerateParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            PreferredLocation = new PreferredLocationCriteria
            {
                Province = "Western",
                District = "Colombo"
            },
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 500m
            }
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.False(string.IsNullOrWhiteSpace(recommendation.Explanation));
        Assert.Contains(parcel.Identifier.CadastralNumber, recommendation.Explanation);
        Assert.True(
            recommendation.Explanation.Contains("Positive factors", StringComparison.OrdinalIgnoreCase)
            || recommendation.Explanation.Contains("Negative factors", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecommendAsync_evaluates_custom_criteria()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil",
                    Name = "Soil Type",
                    ExpectedValue = "Loam",
                    ParcelAttributePath = "characteristics.soiltype",
                    Weight = 0.05m
                }
            ]
        });

        var recommendation = Assert.Single(response.Recommendations);

        Assert.Contains(recommendation.MatchingCriteria, c => c.Key == "custom-criteria");
    }

    private static LandRecommendationSearchRequest CreateRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 5m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        PreferredLocation = new PreferredLocationCriteria
        {
            Province = "Western"
        },
        Accessibility = new AccessibilityCriteria
        {
            RequireRoadAccess = true,
            MaxRoadDistanceMeters = 1000m
        },
        Environmental = new EnvironmentalCriteria(),
        Regulatory = new RegulatoryCriteria()
    };
}
