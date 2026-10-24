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

    [Theory]
    [InlineData(LandUseType.Agricultural)]
    [InlineData(LandUseType.Residential)]
    [InlineData(LandUseType.Commercial)]
    public void Validate_accepts_supported_purposes(LandUseType purpose)
    {
        var result = _validator.Validate(CreateValidRequest() with { RequiredPurpose = purpose });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(LandUseType.Industrial)]
    [InlineData(LandUseType.Tourism)]
    [InlineData(LandUseType.Conservation)]
    [InlineData(LandUseType.MixedUse)]
    [InlineData(LandUseType.Other)]
    [InlineData((LandUseType)123)]
    public void Validate_rejects_unsupported_purposes(LandUseType purpose)
    {
        var result = _validator.Validate(CreateValidRequest() with { RequiredPurpose = purpose });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Required purpose", StringComparison.Ordinal));
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
        Regulatory = new RegulatoryCriteria()
    };
}
