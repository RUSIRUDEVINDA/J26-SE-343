using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class RecommendationScoreCalculator
{
    public static decimal Calculate(IReadOnlyList<CriterionEvaluationDto> evaluations)
    {
        if (evaluations.Count == 0)
        {
            return 0m;
        }

        var totalWeight = evaluations.Sum(e => e.Weight);
        if (totalWeight <= 0m)
        {
            return 0m;
        }

        var weightedSum = evaluations.Sum(e => e.WeightedScore);
        return Math.Round(weightedSum / totalWeight, 2);
    }
}
