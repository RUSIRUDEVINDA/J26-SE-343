using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class LandRecommendationSearchRequestValidatorTests
{
    private readonly LandRecommendationSearchRequestValidator _validator = new();

    [Fact]
    public void Validate_returns_success_for_valid_request()
    {
        var request = CreateValidRequest();

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_fails_for_invalid_custom_criterion_weight()
    {
        var request = CreateValidRequest() with
        {
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil",
                    Name = "Soil Type",
                    Weight = 1.5m,
                    ExpectedValue = "Loam"
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("weight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_fails_for_duplicate_custom_criterion_keys()
    {
        var request = CreateValidRequest() with
        {
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil",
                    Name = "Soil A",
                    ExpectedValue = "Loam"
                },
                new CustomCriterionCriteria
                {
                    Key = "SOIL",
                    Name = "Soil B",
                    ExpectedValue = "Clay"
                }
            ]
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    private static LandRecommendationSearchRequest CreateValidRequest() => new()
    {
        RequiredPurpose = LandUseType.Agricultural,
        RequiredAreaHectares = 5m,
        RequiredLandCategory = LandCategoryType.StateLand,
        RequiredLandUse = LandUseType.Agricultural,
        PreferredLocation = new PreferredLocationCriteria
        {
            Province = "Western",
            District = "Colombo"
        },
        Accessibility = new AccessibilityCriteria
        {
            RequireRoadAccess = true,
            MaxRoadDistanceMeters = 1000m
        },
        Environmental = new EnvironmentalCriteria(),
        Regulatory = new RegulatoryCriteria()
    };
}
