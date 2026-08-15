using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class EnvironmentalCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.10m;
    public string Key => "environmental";
    public int Order => 60;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.Environmental is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var criteria = request.Environmental!;
        var restrictions = parcel.EnvironmentalRestrictions.ToList();

        if (restrictions.Count == 0)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Environmental Requirements",
                CriterionCategory.EnvironmentalRequirement,
                isMet: true,
                score: 100m,
                DefaultWeight,
                "No environmental restrictions are recorded for this parcel.");
        }

        var maxSeverity = restrictions.Max(r => r.Severity);
        var hasProhibitive = restrictions.Any(r => r.Severity == RestrictionSeverity.Prohibitive);

        if (criteria.RejectProhibitiveEnvironmentalRestrictions && hasProhibitive)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Environmental Requirements",
                CriterionCategory.EnvironmentalRequirement,
                isMet: false,
                score: 0m,
                DefaultWeight,
                "Parcel has prohibitive environmental restrictions.");
        }

        if (maxSeverity <= criteria.MaxAllowedEnvironmentalSeverity)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Environmental Requirements",
                CriterionCategory.EnvironmentalRequirement,
                isMet: true,
                score: 85m,
                DefaultWeight,
                $"Highest environmental restriction severity ({maxSeverity}) is within allowed limit ({criteria.MaxAllowedEnvironmentalSeverity}).");
        }

        var severityGap = (int)maxSeverity - (int)criteria.MaxAllowedEnvironmentalSeverity;
        var score = Math.Max(0m, 70m - (severityGap * 25m));

        return CriterionEvaluationFactory.Create(
            Key,
            "Environmental Requirements",
            CriterionCategory.EnvironmentalRequirement,
            isMet: false,
            score,
            DefaultWeight,
            $"Highest environmental restriction severity ({maxSeverity}) exceeds allowed limit ({criteria.MaxAllowedEnvironmentalSeverity}).");
    }
}
