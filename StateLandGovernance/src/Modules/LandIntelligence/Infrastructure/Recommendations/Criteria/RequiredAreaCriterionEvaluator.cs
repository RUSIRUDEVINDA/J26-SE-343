using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class RequiredAreaCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.20m;
    public string Key => "required-area";
    public int Order => 10;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.RequiredAreaHectares is > 0;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var required = request.RequiredAreaHectares!.Value;
        var parcelHectares = parcel.Area.ToHectares();
        var tolerance = required * (request.AreaTolerancePercent / 100m);
        var difference = Math.Abs(parcelHectares - required);

        if (difference <= tolerance)
        {
            var summary = parcelHectares == required
                ? $"Parcel area ({parcelHectares:F2} ha) satisfies the required area."
                : parcelHectares < required
                    ? $"Parcel area ({parcelHectares:F2} ha) satisfies the minimum required area ({required:F2} ha within {request.AreaTolerancePercent}% tolerance)."
                    : $"Parcel area ({parcelHectares:F2} ha) satisfies the required area.";

            return CriterionEvaluationFactory.Create(
                Key,
                "Required Area",
                CriterionCategory.RequiredArea,
                isMet: true,
                score: 100m,
                DefaultWeight,
                summary);
        }

        if (parcelHectares >= required)
        {
            var excessRatio = (parcelHectares - required) / required;
            var score = excessRatio <= 0.5m ? 75m : 50m;
            var summary = excessRatio <= 0.5m
                ? $"Parcel area ({parcelHectares:F2} ha) satisfies the minimum required area ({required:F2} ha) but exceeds the requested size."
                : $"Parcel area ({parcelHectares:F2} ha) satisfies the minimum required area ({required:F2} ha), but substantially exceeds the requested size.";

            return CriterionEvaluationFactory.Create(
                Key,
                "Required Area",
                CriterionCategory.RequiredArea,
                isMet: true,
                score,
                DefaultWeight,
                summary);
        }

        var shortfallRatio = (required - parcelHectares) / required;
        var shortScore = shortfallRatio <= 0.25m ? 40m : 0m;
        return CriterionEvaluationFactory.Create(
            Key,
            "Required Area",
            CriterionCategory.RequiredArea,
            isMet: false,
            shortScore,
            DefaultWeight,
            $"Parcel area ({parcelHectares:F2} ha) is below the minimum required area ({required:F2} ha).");
    }
}
