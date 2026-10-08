using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class LandUseCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.15m;
    public string Key => "land-use";
    public int Order => 30;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.RequiredLandUse is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var required = request.RequiredLandUse!.Value;
        var currentUse = parcel.CurrentUse?.Type;

        if (currentUse == required)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Land Use",
                CriterionCategory.LandUseMatch,
                isMet: true,
                score: 100m,
                DefaultWeight,
                $"Current land use ({required}) matches required use.");
        }

        if (currentUse is null)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Land Use",
                CriterionCategory.LandUseMatch,
                isMet: false,
                score: 50m,
                DefaultWeight,
                $"Land use is not recorded; required use is {required}.");
        }

        return CriterionEvaluationFactory.Create(
            Key,
            "Land Use",
            CriterionCategory.LandUseMatch,
            isMet: false,
            score: 20m,
            DefaultWeight,
            $"Current land use ({currentUse}) differs from required use ({required}).");
    }
}
