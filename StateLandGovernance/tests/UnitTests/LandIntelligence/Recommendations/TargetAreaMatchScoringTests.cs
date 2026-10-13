using StateLandGovernance.LandIntelligence.Application.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class TargetAreaMatchScoringTests
{
    [Theory]
    [InlineData(0.05, 0.05, 100)]
    [InlineData(0.045, 0.05, 90)]
    [InlineData(0.055, 0.05, 90)]
    public void ComputeScore_matches_continuous_target_area_formula(
        double actualHa,
        double targetHa,
        double expectedScore)
    {
        var score = TargetAreaMatchScoring.ComputeScore(
            Convert.ToDecimal(actualHa),
            Convert.ToDecimal(targetHa));

        Assert.Equal(Convert.ToDecimal(expectedScore), score);
    }
}
