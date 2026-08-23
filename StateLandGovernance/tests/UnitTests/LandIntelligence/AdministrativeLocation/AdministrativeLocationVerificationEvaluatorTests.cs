using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class AdministrativeLocationVerificationEvaluatorTests
{
    [Fact]
    public void ResolveStatus_returns_Verified_when_both_administrative_names_match()
    {
        var status = AdministrativeLocationVerificationEvaluator.ResolveStatus(
            "Hambantota",
            "Southern",
            districtMatches: true,
            provinceMatches: true);

        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, status);
    }

    [Fact]
    public void ResolveStatus_returns_Mismatch_when_detected_values_exist_but_do_not_match()
    {
        var status = AdministrativeLocationVerificationEvaluator.ResolveStatus(
            "Hambantota",
            "Southern",
            districtMatches: false,
            provinceMatches: true);

        Assert.Equal(AdministrativeLocationVerificationStatus.Mismatch, status);
    }

    [Theory]
    [InlineData(null, "Southern")]
    [InlineData("Hambantota", null)]
    [InlineData("", "Southern")]
    [InlineData("Hambantota", "")]
    public void ResolveStatus_returns_Unavailable_when_gis_detection_is_missing(
        string? detectedDistrict,
        string? detectedProvince)
    {
        var status = AdministrativeLocationVerificationEvaluator.ResolveStatus(
            detectedDistrict,
            detectedProvince,
            districtMatches: false,
            provinceMatches: false);

        Assert.Equal(AdministrativeLocationVerificationStatus.Unavailable, status);
    }

    [Fact]
    public void CompareLocations_returns_null_match_flags_when_detection_is_unavailable()
    {
        var comparison = AdministrativeLocationVerificationEvaluator.CompareLocations(
            "Kandy",
            "Central Province",
            detectedDistrict: null,
            detectedProvince: null);

        Assert.Null(comparison.DistrictMatches);
        Assert.Null(comparison.ProvinceMatches);
    }
}
