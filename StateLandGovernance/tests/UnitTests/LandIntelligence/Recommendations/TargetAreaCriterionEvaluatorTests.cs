using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class TargetAreaCriterionEvaluatorTests
{
    private readonly TargetAreaCriterionEvaluator _evaluator = new();

    [Fact]
    public void IsApplicable_when_target_area_set_via_legacy_required_field()
    {
        Assert.True(_evaluator.IsApplicable(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            RequiredAreaHectares = 0.05m
        }));
    }

    [Theory]
    [InlineData(0.05, 100)]
    [InlineData(0.045, 90)]
    [InlineData(0.055, 90)]
    public void Evaluate_uses_unrounded_hectares_for_scoring(decimal parcelHa, decimal expectedScore)
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(parcelHa);

        var evaluation = _evaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                TargetAreaHectares = 0.05m
            });

        Assert.True(evaluation.WasAssessed);
        Assert.Equal(expectedScore, evaluation.Score);
        Assert.Contains("parcel area", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unrounded for scoring", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
