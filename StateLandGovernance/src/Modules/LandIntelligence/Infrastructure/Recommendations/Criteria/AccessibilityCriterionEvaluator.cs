using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class AccessibilityCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.10m;
    public string Key => "accessibility";
    public int Order => 50;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.Accessibility is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var criteria = request.Accessibility!;
        var roadFeatures = parcel.InfrastructureFeatures
            .Where(f => f.Type is InfrastructureFeatureType.Road or InfrastructureFeatureType.Railway)
            .ToList();

        if (criteria.RequireRoadAccess && roadFeatures.Count == 0)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Accessibility",
                CriterionCategory.AccessibilityRequirement,
                isMet: false,
                score: 0m,
                DefaultWeight,
                "No road or railway infrastructure is recorded for this parcel.",
                ParcelAttributeProvenanceResolver.ResolveRoadDistanceProvenance(null),
                "infrastructure.road.distanceMeters");
        }

        if (criteria.MaxRoadDistanceMeters is null or <= 0)
        {
            var hasRoad = roadFeatures.Count > 0;
            var referenceFeature = roadFeatures.FirstOrDefault();
            return CriterionEvaluationFactory.Create(
                Key,
                "Accessibility",
                CriterionCategory.AccessibilityRequirement,
                isMet: !criteria.RequireRoadAccess || hasRoad,
                score: hasRoad ? 100m : 50m,
                DefaultWeight,
                hasRoad
                    ? "Infrastructure access is recorded near the parcel."
                    : "No infrastructure proximity data is recorded.",
                referenceFeature is null
                    ? AttributeProvenance.Unknown("Road access")
                    : ParcelAttributeProvenanceResolver.ResolveRoadDistanceProvenance(referenceFeature),
                "infrastructure.road.distanceMeters");
        }

        var nearestRoad = roadFeatures
            .Where(f => f.DistanceMeters.HasValue)
            .MinBy(f => f.DistanceMeters);

        if (nearestRoad?.DistanceMeters is null)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Accessibility",
                CriterionCategory.AccessibilityRequirement,
                isMet: false,
                score: 40m,
                DefaultWeight,
                "Road distance is not recorded; accessibility could not be fully verified.",
                AttributeProvenance.Unknown("Road distance"),
                "infrastructure.road.distanceMeters");
        }

        var distance = nearestRoad.DistanceMeters.Value;
        var maxDistance = criteria.MaxRoadDistanceMeters.Value;
        var withinLimit = distance <= maxDistance;
        var roadProvenance = ParcelAttributeProvenanceResolver.ResolveRoadDistanceProvenance(nearestRoad);

        decimal score;
        if (withinLimit)
        {
            score = 100m;
        }
        else if (distance <= maxDistance * 1.5m)
        {
            score = 60m;
        }
        else
        {
            score = 25m;
        }

        return CriterionEvaluationFactory.Create(
            Key,
            "Accessibility",
            CriterionCategory.AccessibilityRequirement,
            isMet: withinLimit,
            score,
            DefaultWeight,
            withinLimit
                ? $"Nearest road ({nearestRoad.Name}) is {distance:F0} m away (within {maxDistance:F0} m limit)."
                : $"Nearest road ({nearestRoad.Name}) is {distance:F0} m away (exceeds {maxDistance:F0} m limit).",
            roadProvenance,
            "infrastructure.road.distanceMeters");
    }
}
