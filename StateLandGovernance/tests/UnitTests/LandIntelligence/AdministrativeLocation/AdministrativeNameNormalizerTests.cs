using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class AdministrativeNameNormalizerTests
{
    [Theory]
    [InlineData("Hambantota", "Hambantota", true)]
    [InlineData("Southern Province", "Southern", true)]
    [InlineData("Southern", "Southern Province", true)]
    [InlineData("  hambantota ", "HAMBANTOTA", true)]
    [InlineData("Matara District", "Matara", true)]
    public void NamesMatch_tolerates_harmless_administrative_suffix_and_case_differences(
        string stored,
        string detected,
        bool expected)
    {
        Assert.Equal(expected, AdministrativeNameNormalizer.NamesMatch(stored, detected));
    }

    [Theory]
    [InlineData("Kandy", "Hambantota")]
    [InlineData("Western Province", "Southern")]
    [InlineData("", "Southern")]
    [InlineData("Southern", "")]
    public void NamesMatch_does_not_equate_genuinely_different_locations(string stored, string detected)
    {
        Assert.False(AdministrativeNameNormalizer.NamesMatch(stored, detected));
    }

    [Fact]
    public void Normalize_strips_province_and_district_suffixes()
    {
        Assert.Equal("SOUTHERN", AdministrativeNameNormalizer.Normalize("Southern Province"));
        Assert.Equal("HAMBANTOTA", AdministrativeNameNormalizer.Normalize("Hambantota District"));
    }
}
