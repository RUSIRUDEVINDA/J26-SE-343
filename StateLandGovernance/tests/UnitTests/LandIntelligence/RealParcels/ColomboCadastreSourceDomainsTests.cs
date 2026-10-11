using StateLandGovernance.LandIntelligence.Application.RealParcels;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class ColomboCadastreSourceDomainsTests
{
    [Theory]
    [InlineData("2", "Garden")]
    [InlineData("73", "Paddy Field")]
    [InlineData("52", "Means of Access")]
    public void TryDecodeLandUseEnglishLabel_returns_source_domain_labels(string code, string expected) =>
        Assert.Equal(expected, ColomboCadastreSourceDomains.TryDecodeLandUseEnglishLabel(code));

    [Fact]
    public void FormatUnknownLandUseCode_does_not_invent_labels() =>
        Assert.Equal(
            "Source code 9999 — label unavailable",
            ColomboCadastreSourceDomains.FormatSourceReportedLandUseDisplay("9999", null));

    [Theory]
    [InlineData("3", "Private")]
    [InlineData("2", "State")]
    public void TryDecodeClaimantCategoryEnglishLabel_returns_source_domain_labels(string code, string expected) =>
        Assert.Equal(expected, ColomboCadastreSourceDomains.TryDecodeClaimantCategoryEnglishLabel(code));
}
