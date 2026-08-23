using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class CustomCriteriaEvaluator : IRecommendationCriterionEvaluator
{
    public string Key => "custom-criteria";
    public int Order => 90;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.AdditionalCriteria is { Count: > 0 };

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var customCriteria = request.AdditionalCriteria!;
        var results = customCriteria
            .Select(c => EvaluateSingle(parcel, c))
            .ToList();

        var averageScore = results.Count == 0 ? 100m : results.Average(r => r.Score);
        var allMet = results.All(r => r.IsMet);
        var totalWeight = customCriteria.Sum(c => c.Weight);
        var summary = allMet
            ? $"All {results.Count} custom criteria were satisfied."
            : $"{results.Count(r => r.IsMet)}/{results.Count} custom criteria were satisfied.";

        var primaryCriterion = customCriteria[0];
        var primaryProvenance = ResolveProvenanceForCriterion(parcel, primaryCriterion.ParcelAttributePath);

        return CriterionEvaluationFactory.Create(
            Key,
            "Custom Criteria",
            CriterionCategory.CustomCriterion,
            allMet,
            averageScore,
            totalWeight > 0 ? totalWeight : 0.05m,
            summary,
            primaryProvenance,
            primaryCriterion.ParcelAttributePath);
    }

    private static (bool IsMet, decimal Score) EvaluateSingle(LandParcel parcel, CustomCriterionCriteria criterion)
    {
        var actual = ResolveAttributeValue(parcel, criterion.ParcelAttributePath);
        if (actual is null)
        {
            return (false, 0m);
        }

        var matches = string.Equals(
            actual.Trim(),
            criterion.ExpectedValue.Trim(),
            StringComparison.OrdinalIgnoreCase);

        return (matches, matches ? 100m : 0m);
    }

    private static AttributeProvenance ResolveProvenanceForCriterion(LandParcel parcel, string? attributePath)
    {
        var value = ResolveAttributeValue(parcel, attributePath);
        if (value is null)
        {
            return AttributeProvenance.Unknown(attributePath);
        }

        var stored = ParcelAttributeProvenanceResolver.ResolveCharacteristicProvenance(parcel, attributePath);
        return ParcelAttributeProvenanceResolver.ResolveOrUnknown(stored);
    }

    private static string? ResolveAttributeValue(LandParcel parcel, string? attributePath)
    {
        if (string.IsNullOrWhiteSpace(attributePath))
        {
            return parcel.Category.Type.ToString();
        }

        return attributePath.Trim().ToLowerInvariant() switch
        {
            "category" or "category.type" => parcel.Category.Type.ToString(),
            "currentuse" or "currentuse.type" => parcel.CurrentUse?.Type.ToString(),
            "location.province" => parcel.Location.Province,
            "location.district" => parcel.Location.District,
            "characteristics.soiltype" => parcel.Characteristics?.SoilType,
            "characteristics.terraindescription" => parcel.Characteristics?.TerrainDescription,
            "characteristics.elevationmeters" => parcel.Characteristics?.ElevationMeters?.ToString(),
            _ => null
        };
    }
}
