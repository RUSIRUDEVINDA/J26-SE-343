using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class AccessibilityCriterionEvaluatorTests
{
    private readonly AccessibilityCriterionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_marks_parcel_within_max_road_distance_as_met()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(3000m);

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.True(result.IsMet);
        Assert.Contains("3000", result.Summary);
    }

    [Fact]
    public void Evaluate_marks_parcel_beyond_max_road_distance_as_not_met()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(8000m);

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.False(result.IsMet);
        Assert.Contains("8000", result.Summary);
    }

    [Fact]
    public void Evaluate_ignores_non_road_infrastructure_when_only_hospital_is_nearby()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithInfrastructureFeatures(
            "SYNTH-HOSPITAL-ONLY",
            (InfrastructureFeatureType.WaterSupply, "[SYNTHETIC] Hospital", 500m),
            (InfrastructureFeatureType.Road, "[SYNTHETIC] Access Road", 8000m));

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.False(result.IsMet);
        Assert.Contains("Access Road", result.Summary);
        Assert.DoesNotContain("Hospital", result.Summary);
    }

    [Fact]
    public void Evaluate_counts_railway_toward_max_road_distance()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithInfrastructureFeatures(
            "SYNTH-RAIL-3KM",
            (InfrastructureFeatureType.Railway, "[SYNTHETIC] Railway Line", 3000m));

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.True(result.IsMet);
        Assert.Contains("Railway Line", result.Summary);
    }

    [Fact]
    public void Evaluate_fails_when_no_road_or_railway_distance_is_recorded()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithInfrastructureFeatures(
            "SYNTH-NO-ROAD-DATA",
            (InfrastructureFeatureType.WaterSupply, "[SYNTHETIC] Hospital", 500m));

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.False(result.IsMet);
        Assert.Contains("Road distance is not recorded", result.Summary);
    }

    [Fact]
    public void Evaluate_uses_nearest_road_when_mixed_infrastructure_is_present()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithInfrastructureFeatures(
            "SYNTH-MIXED-PASS",
            (InfrastructureFeatureType.WaterSupply, "[SYNTHETIC] Hospital", 500m),
            (InfrastructureFeatureType.Road, "[SYNTHETIC] Access Road", 3000m));

        var result = _evaluator.Evaluate(parcel, CreateRequest(maxRoadDistanceMeters: 5000m));

        Assert.True(result.IsMet);
        Assert.Contains("Access Road", result.Summary);
    }

    private static LandRecommendationSearchRequest CreateRequest(decimal maxRoadDistanceMeters) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            Accessibility = new AccessibilityCriteria
            {
                MaxRoadDistanceMeters = maxRoadDistanceMeters
            }
        };
}
