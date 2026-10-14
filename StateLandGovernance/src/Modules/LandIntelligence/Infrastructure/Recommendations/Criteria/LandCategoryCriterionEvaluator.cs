using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class LandCategoryCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.15m;
    public string Key => "land-category";
    public int Order => 20;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.RequiredLandCategory is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var required = request.RequiredLandCategory!.Value;
        var matches = parcel.Category.Type == required;

        return CriterionEvaluationFactory.Create(
            Key,
            "Land Category",
            CriterionCategory.LandCategoryMatch,
            matches,
            matches ? 100m : 0m,
            DefaultWeight,
            matches
                ? $"Parcel category ({parcel.Category.Type}) matches required category ({required})."
                : $"Parcel category ({parcel.Category.Type}) does not match required category ({required}).");
    }
}
