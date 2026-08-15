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
        var parcelHectares = ConvertToHectares(parcel.Area.Value, parcel.Area.Unit);
        var tolerance = required * (request.AreaTolerancePercent / 100m);
        var difference = Math.Abs(parcelHectares - required);

        if (difference <= tolerance)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Required Area",
                CriterionCategory.RequiredArea,
                isMet: true,
                score: 100m,
                DefaultWeight,
                $"Parcel area ({parcelHectares:F2} ha) satisfies required area ({required:F2} ha within {request.AreaTolerancePercent}% tolerance).");
        }

        if (parcelHectares >= required)
        {
            var excessRatio = (parcelHectares - required) / required;
            var score = excessRatio <= 0.5m ? 75m : 50m;
            return CriterionEvaluationFactory.Create(
                Key,
                "Required Area",
                CriterionCategory.RequiredArea,
                isMet: score >= 60m,
                score,
                DefaultWeight,
                $"Parcel area ({parcelHectares:F2} ha) exceeds required area ({required:F2} ha).");
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
            $"Parcel area ({parcelHectares:F2} ha) is below required area ({required:F2} ha).");
    }

    private static decimal ConvertToHectares(decimal value, AreaUnit unit) => unit switch
    {
        AreaUnit.Hectares => value,
        AreaUnit.Acres => value * 0.404686m,
        AreaUnit.SquareMeters => value / 10_000m,
        _ => value
    };
}
