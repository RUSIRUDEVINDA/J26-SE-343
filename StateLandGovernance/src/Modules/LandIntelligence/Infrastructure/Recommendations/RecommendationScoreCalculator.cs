using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class RecommendationScoreCalculator
{
    public static decimal Calculate(IReadOnlyList<CriterionEvaluationDto> evaluations)
    {
        var assessed = evaluations.Where(e => e.WasAssessed).ToList();
        if (assessed.Count == 0)
        {
            return 0m;
        }

        var totalWeight = assessed.Sum(e => e.Weight);
        if (totalWeight <= 0m)
        {
            return 0m;
        }

        var weightedSum = assessed.Sum(e => e.WeightedScore);
        return Math.Round(weightedSum / totalWeight, 2);
    }
}
