using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class RecommendationExplanationBuilder
{
    public static string Build(
        string cadastralNumber,
        LandUseType requiredPurpose,
        decimal suitabilityScore,
        IReadOnlyList<CriterionEvaluationDto> matchingCriteria,
        IReadOnlyList<CriterionEvaluationDto> failedCriteria,
        IReadOnlyList<RestrictionSummaryDto> restrictions,
        IReadOnlyList<string>? supplementarySummaries = null)
    {
        var positiveFactors = matchingCriteria
            .Select(c => c.Summary)
            .Distinct()
            .Take(4)
            .ToList();

        var negativeFactors = failedCriteria
            .Select(c => c.Summary)
            .Distinct()
            .Take(4)
            .ToList();

        var restrictionSummaries = restrictions
            .Where(r => r.Severity >= RestrictionSeverity.Medium)
            .Select(r => r.Description)
            .Distinct()
            .Take(3)
            .ToList();

        var scoreDescriptor = suitabilityScore switch
        {
            >= 80m => "high",
            >= 60m => "moderate",
            >= 40m => "low",
            _ => "very low"
        };

        var explanation = $"Parcel {cadastralNumber} received a {scoreDescriptor} suitability score ({suitabilityScore:F0}/100) for {requiredPurpose}.";

        if (positiveFactors.Count > 0)
        {
            explanation += " Positive factors: " + string.Join("; ", positiveFactors) + ".";
        }

        if (negativeFactors.Count > 0)
        {
            explanation += " Negative factors: " + string.Join("; ", negativeFactors) + ".";
        }

        if (restrictionSummaries.Count > 0)
        {
            explanation += " Restrictions: " + string.Join("; ", restrictionSummaries) + ".";
        }

        if (supplementarySummaries is { Count: > 0 })
        {
            explanation += " GIS context: " + string.Join("; ", supplementarySummaries) + ".";
        }

        if (suitabilityScore >= 80m)
        {
            explanation += " This parcel is a strong candidate based on the configured criteria.";
        }
        else if (suitabilityScore >= 60m)
        {
            explanation += " This parcel may be suitable with further review of failed criteria and restrictions.";
        }
        else
        {
            explanation += " This parcel has significant gaps against the configured criteria.";
        }

        return explanation;
    }
}
