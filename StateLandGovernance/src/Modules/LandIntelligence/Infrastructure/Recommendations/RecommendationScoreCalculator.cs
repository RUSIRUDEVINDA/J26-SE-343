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



        if (assessed.All(e => e.Score == 100m))

        {

            return 100m;

        }



        var weightedSum = assessed.Sum(e => e.WeightedScore);

        var average = weightedSum / totalWeight;



        // Truncate toward zero so rounding never displays 100 unless every assessed criterion is exactly 100.

        var truncated = Math.Truncate(average * 100m) / 100m;

        return truncated >= 100m ? 99.99m : truncated;

    }

}


