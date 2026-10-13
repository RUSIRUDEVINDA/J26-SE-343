using StateLandGovernance.LandIntelligence.Application.DTOs;
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

    private static CriterionEvaluationDto Criterion(decimal score, decimal weight) =>
        new(
            "test",
            "Test",
            CriterionCategory.CustomCriterion,
            score >= 50m,
            score,
            weight,
            score * weight,
            "test");
}
