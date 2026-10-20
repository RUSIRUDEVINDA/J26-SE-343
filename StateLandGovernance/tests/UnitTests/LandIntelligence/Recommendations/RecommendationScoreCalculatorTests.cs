using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class RecommendationScoreCalculatorTests
{
    [Fact]
    public void Calculate_returns_100_only_when_every_assessed_criterion_is_exactly_100()
    {
        var perfect = new[]
        {
            Criterion(100m, 0.15m),
            Criterion(100m, 0.10m),
        };

        Assert.Equal(100m, RecommendationScoreCalculator.Calculate(perfect));
    }

    [Fact]
    public void Calculate_truncates_weighted_average_and_never_rounds_up_to_100()
    {
        var mixed = new[]
        {
            Criterion(100m, 0.15m),
            Criterion(100m, 0.10m),
            Criterion(90m, 0.20m),
        };

        var score = RecommendationScoreCalculator.Calculate(mixed);
        Assert.Equal(95.55m, score);
        Assert.NotEqual(100m, score);
    }

    [Fact]
    public void Exact_and_near_target_area_examples_drive_total_below_100_when_area_is_90()
    {
        var exactArea = TargetAreaMatchScoring.ComputeScore(0.05m, 0.05m);
        var nearArea = TargetAreaMatchScoring.ComputeScore(0.045m, 0.05m);

        Assert.Equal(100m, exactArea);
        Assert.Equal(90m, nearArea);

        var withExactArea = new[]
        {
            Criterion(exactArea, 0.20m),
            Criterion(100m, 0.15m),
            Criterion(100m, 0.10m),
        };
        Assert.Equal(100m, RecommendationScoreCalculator.Calculate(withExactArea));

        var withNearArea = new[]
        {
            Criterion(nearArea, 0.20m),
            Criterion(100m, 0.15m),
            Criterion(100m, 0.10m),
        };
        var total = RecommendationScoreCalculator.Calculate(withNearArea);
        Assert.True(total < 100m);
        Assert.Equal(95.55m, total);
    }

    [Fact]
    public void Missing_evidence_not_assessed_does_not_count_as_positive_or_block_perfect_100()
    {
        var evaluations = new[]
        {
            Criterion(100m, 0.15m),
            Criterion(100m, 0.10m),
            NotAssessed("spatial-constraints", 0.10m),
        };

        Assert.Equal(100m, RecommendationScoreCalculator.Calculate(evaluations));
    }

    [Fact]
    public void Missing_evidence_not_assessed_does_not_inflate_score_when_other_criteria_partial()
    {
        var evaluations = new[]
        {
            Criterion(90m, 0.20m),
            Criterion(100m, 0.10m),
            NotAssessed("spatial-constraints", 0.50m),
        };

        var score = RecommendationScoreCalculator.Calculate(evaluations);
        Assert.True(score < 100m);
        Assert.Equal(93.33m, score);
    }

    private static CriterionEvaluationDto Criterion(decimal score, decimal weight) =>
        new(
            "test",
            "Test",
            CriterionCategory.CustomCriterion,
            score >= 50m,
            score,
            weight,
            score * weight,
            "test",
            WasAssessed: true);

    private static CriterionEvaluationDto NotAssessed(string key, decimal weight) =>
        new(
            key,
            "Not assessed criterion",
            CriterionCategory.SpatialConstraintImpact,
            IsMet: false,
            Score: 0m,
            Weight: weight,
            WeightedScore: 0m,
            Summary: "Missing environmental evidence",
            WasAssessed: false,
            NotAssessedReason: "Spatial constraint GIS coverage missing");
}
