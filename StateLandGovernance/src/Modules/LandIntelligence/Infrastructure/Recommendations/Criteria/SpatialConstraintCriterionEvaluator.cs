using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class SpatialConstraintCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.10m;
    public string Key => "spatial-constraints";
    public int Order => 80;

    public bool IsApplicable(LandRecommendationSearchRequest request) => true;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var constraints = parcel.SpatialConstraints.ToList();

        if (constraints.Count == 0)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Spatial Constraints",
                CriterionCategory.SpatialConstraintImpact,
                isMet: true,
                score: 100m,
                DefaultWeight,
                "No spatial constraints are recorded for this parcel.");
        }

        var maxSeverity = constraints.Max(c => c.Severity);

        if (maxSeverity == RestrictionSeverity.Prohibitive)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Spatial Constraints",
                CriterionCategory.SpatialConstraintImpact,
                isMet: false,
                score: 10m,
                DefaultWeight,
                "Parcel has prohibitive spatial constraints.");
        }

        var score = maxSeverity switch
        {
            RestrictionSeverity.Low => 90m,
            RestrictionSeverity.Medium => 70m,
            RestrictionSeverity.High => 45m,
            _ => 30m
        };

        return CriterionEvaluationFactory.Create(
            Key,
            "Spatial Constraints",
            CriterionCategory.SpatialConstraintImpact,
            isMet: maxSeverity <= RestrictionSeverity.Medium,
            score,
            DefaultWeight,
            $"{constraints.Count} spatial constraint(s) recorded; highest severity is {maxSeverity}.");
    }
}
