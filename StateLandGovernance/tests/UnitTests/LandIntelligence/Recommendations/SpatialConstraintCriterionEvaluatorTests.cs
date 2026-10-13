using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class SpatialConstraintCriterionEvaluatorTests
{
    private readonly SpatialConstraintCriterionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_colombo_like_parcel_without_gis_enrichment_is_not_assessed()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("COLOMBO-300-NO-GIS");

        var evaluation = Evaluate(parcel);

        Assert.False(evaluation.WasAssessed);
        Assert.Equal(0m, evaluation.WeightedScore);
        Assert.Contains("No mapped conservation", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No mapped intersection", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_complete_gis_enrichment_without_intersection_is_assessed_clear()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithCompleteGisSpatialCoverage(
            "GIS-CLEAR-001");

        var evaluation = Evaluate(parcel);

        Assert.True(evaluation.WasAssessed);
        Assert.True(evaluation.IsMet);
        Assert.Equal(100m, evaluation.Score);
        Assert.Contains("assessed", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no intersection", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_recorded_spatial_constraint_uses_severity_scoring()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateModerateParcel();

        var evaluation = Evaluate(parcel);

        Assert.True(evaluation.WasAssessed);
        Assert.Equal(70m, evaluation.Score);
        Assert.Contains("spatial constraint", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_gis_conservation_intersection_is_assessed_without_fake_clear_result()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisConservationRestriction();

        var evaluation = Evaluate(parcel);

        Assert.True(evaluation.WasAssessed);
        Assert.Equal(90m, evaluation.Score);
        Assert.Contains("mapped GIS", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recommendation_score_colombo_like_missing_gis_does_not_include_spatial_weight()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("COLOMBO-SCORE-A");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });

        var recommendation = Assert.Single(response.Recommendations);
        var spatial = recommendation.FailedCriteria
            .Concat(recommendation.MatchingCriteria)
            .First(c => c.Key == "spatial-constraints");

        Assert.False(spatial.WasAssessed);
        Assert.NotNull(response.Transparency);
        Assert.Contains(
            response.Transparency.UnavailableCriteria,
            u => u.Key.Equals("spatial-constraints", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recommendation_partial_gis_without_spatial_hits_does_not_award_spatial_clear_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(
            3000m,
            "COLOMBO-PARTIAL-GIS");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id });
        var spatial = response.Recommendations[0].FailedCriteria
            .Concat(response.Recommendations[0].MatchingCriteria)
            .First(c => c.Key == "spatial-constraints");

        Assert.False(spatial.WasAssessed);
    }

    [Fact]
    public async Task Colombo_like_parcel_old_spatial_clear_would_inflate_overall_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(
            14.8m,
            "COLOMBO-300-SCORE");
        var engine = RecommendationEngineTestFactory.Create(parcel);

        var response = await engine.RecommendAsync(CreateRequest() with
        {
            TargetParcelId = parcel.Id,
            RequiredAreaHectares = 10m
        });

        var recommendation = Assert.Single(response.Recommendations);
        var criteria = recommendation.MatchingCriteria.Concat(recommendation.FailedCriteria).ToList();
        var spatial = criteria.First(c => c.Key == "spatial-constraints");

        Assert.False(spatial.WasAssessed);

        var assessedOnly = criteria.Where(c => c.WasAssessed).ToList();
        var correctedScore = recommendation.SuitabilityScore;

        var legacySpatialBonus = new CriterionEvaluationDto(
            spatial.Key,
            spatial.Name,
            spatial.Category,
            IsMet: true,
            Score: 100m,
            Weight: SpatialConstraintCriterionEvaluator.DefaultWeight,
            WeightedScore: 100m * SpatialConstraintCriterionEvaluator.DefaultWeight,
            Summary: "No mapped intersection found in available layer.",
            WasAssessed: true);

        var inflatedScore = RecommendationScoreCalculator.Calculate(
            assessedOnly.Append(legacySpatialBonus).ToList());

        Assert.Contains(criteria, c => c.Key == "target-area");
        Assert.True(correctedScore < 100m);
        Assert.InRange(inflatedScore, correctedScore, 100m);
    }

    [Fact]
    public async Task Recommendation_recorded_spatial_constraint_remains_assessed_with_partial_gis()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithGisDerivedRoad(
            3000m,
            "COLOMBO-PARTIAL-GIS-SETBACK");
        parcel.AddSpatialConstraint(new SpatialConstraint(
            SpatialConstraintType.Setback,
            "[SYNTHETIC] Setback requirement",
            RestrictionSeverity.High));

        var engine = RecommendationEngineTestFactory.Create(parcel);
        var recommendation = (await engine.RecommendAsync(CreateRequest() with { TargetParcelId = parcel.Id }))
            .Recommendations[0];

        var spatial = recommendation.FailedCriteria
            .Concat(recommendation.MatchingCriteria)
            .First(c => c.Key == "spatial-constraints");

        Assert.True(spatial.WasAssessed);
        Assert.Equal(45m, spatial.Score);
    }

    private CriterionEvaluationDto Evaluate(LandParcel parcel) =>
        _evaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                RequiredAreaHectares = 1m,
                RequiredLandCategory = LandCategoryType.StateLand,
                RequiredLandUse = LandUseType.Agricultural
            });

    private static LandRecommendationSearchRequest CreateRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 1m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        MaxResults = 5
    };
}
