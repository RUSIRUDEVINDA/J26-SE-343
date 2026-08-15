using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class PurposeAlignmentCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.15m;
    public string Key => "purpose-alignment";
    public int Order => 15;

    public bool IsApplicable(LandRecommendationSearchRequest request) => true;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var purpose = request.RequiredPurpose;
        var currentUse = parcel.CurrentUse?.Type;

        if (currentUse == purpose)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Purpose Alignment",
                CriterionCategory.LandUseMatch,
                isMet: true,
                score: 100m,
                DefaultWeight,
                $"Parcel current use ({purpose}) aligns with requested purpose.");
        }

        if (IsCompatibleUse(currentUse, purpose))
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Purpose Alignment",
                CriterionCategory.LandUseMatch,
                isMet: true,
                score: 70m,
                DefaultWeight,
                $"Parcel use ({currentUse?.ToString() ?? "unspecified"}) is compatible with requested purpose ({purpose}).");
        }

        return CriterionEvaluationFactory.Create(
            Key,
            "Purpose Alignment",
            CriterionCategory.LandUseMatch,
            isMet: false,
            score: 30m,
            DefaultWeight,
            $"Parcel use ({currentUse?.ToString() ?? "unspecified"}) has limited alignment with requested purpose ({purpose}).");
    }

    private static bool IsCompatibleUse(LandUseType? current, LandUseType purpose) => (current, purpose) switch
    {
        (LandUseType.MixedUse, _) => true,
        (_, LandUseType.MixedUse) => true,
        (LandUseType.Agricultural, LandUseType.Tourism) => true,
        (LandUseType.Tourism, LandUseType.Agricultural) => true,
        (LandUseType.Commercial, LandUseType.Industrial) => true,
        (LandUseType.Industrial, LandUseType.Commercial) => true,
        _ => false
    };
}
