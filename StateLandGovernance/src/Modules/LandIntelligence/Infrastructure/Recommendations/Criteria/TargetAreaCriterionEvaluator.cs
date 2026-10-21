using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class TargetAreaCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.20m;
    public string Key => "target-area";
    public int Order => 10;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.ResolveTargetAreaHectares() is > 0;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var target = request.ResolveTargetAreaHectares()!.Value;
        var actual = parcel.Area.ToHectares();
        var score = TargetAreaMatchScoring.ComputeScore(actual, target);
        var isMet = score > 0m;

        var summary =
            $"Target area {FormatHa(target)}; parcel area {FormatHa(actual)} (unrounded for scoring). " +
            $"Area match score {FormatScore(score)} (100 × (1 − |actual − target| / target), floored at 0).";

        return CriterionEvaluationFactory.Create(
            Key,
            "Target Area Match",
            CriterionCategory.RequiredArea,
            isMet,
            score,
            DefaultWeight,
            summary,
            attributePath: "parcel.area");
    }

    private static string FormatHa(decimal hectares) =>
        hectares.ToString("0.####################", System.Globalization.CultureInfo.InvariantCulture) + " ha";

    private static string FormatScore(decimal score) =>
        score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
