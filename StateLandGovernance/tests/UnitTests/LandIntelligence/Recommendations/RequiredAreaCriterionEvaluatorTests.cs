using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class RequiredAreaCriterionEvaluatorTests
{
    private readonly RequiredAreaCriterionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_exact_minimum_is_met_with_full_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(10m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m);

        Assert.True(evaluation.IsMet);
        Assert.Equal(100m, evaluation.Score);
        Assert.Contains("satisfies the required area", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_moderately_larger_parcel_is_met_with_reduced_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(14.8m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m);

        Assert.True(evaluation.IsMet);
        Assert.Equal(75m, evaluation.Score);
        Assert.Contains("satisfies the minimum required area", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exceeds the requested size", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_substantially_larger_parcel_is_met_with_lowest_oversize_score()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(22.5m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m);

        Assert.True(evaluation.IsMet);
        Assert.Equal(50m, evaluation.Score);
        Assert.Contains("substantially exceeds the requested size", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_parcel_below_minimum_is_not_met()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(7m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m);

        Assert.False(evaluation.IsMet);
        Assert.Contains("below the minimum required area", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_parcel_at_tolerance_boundary_is_met()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(8.5m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m, areaTolerancePercent: 15m);

        Assert.True(evaluation.IsMet);
        Assert.Equal(100m, evaluation.Score);
    }

    [Fact]
    public void Evaluate_parcel_just_below_tolerance_boundary_is_not_met()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithArea(8.4m);

        var evaluation = Evaluate(parcel, requiredAreaHectares: 10m, areaTolerancePercent: 15m);

        Assert.False(evaluation.IsMet);
        Assert.Contains("below the minimum required area", evaluation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_converts_acres_to_hectares()
    {
        var parcel = CreateParcel(new LandArea(2m, AreaUnit.Acres), "SYNTH-ACRES-001");

        var evaluation = Evaluate(parcel, requiredAreaHectares: 0.81m);

        Assert.True(evaluation.IsMet);
        Assert.Contains("0.81 ha", evaluation.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_converts_square_meters_to_hectares()
    {
        var parcel = CreateParcel(new LandArea(10_000m, AreaUnit.SquareMeters), "SYNTH-SQM-001");

        var evaluation = Evaluate(parcel, requiredAreaHectares: 1m);

        Assert.True(evaluation.IsMet);
        Assert.Contains("1.00 ha", evaluation.Summary, StringComparison.Ordinal);
    }

    private static LandParcel CreateParcel(LandArea area, string cadastralNumber) =>
        new(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            area,
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"));

    private CriterionEvaluationDto Evaluate(
        LandParcel parcel,
        decimal requiredAreaHectares,
        decimal areaTolerancePercent = 15m) =>
        _evaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                RequiredAreaHectares = requiredAreaHectares,
                AreaTolerancePercent = areaTolerancePercent
            });
}
