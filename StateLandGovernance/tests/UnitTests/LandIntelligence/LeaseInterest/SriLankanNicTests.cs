using StateLandGovernance.LandIntelligence.Application.LeaseInterest;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class SriLankanNicTests
{
    [Theory]
    [InlineData("123456789V", "123456789V")]
    [InlineData("123456789v", "123456789V")]
    [InlineData("123 456 789 V", "123456789V")]
    [InlineData("200123456789", "200123456789")]
    [InlineData("2001-2345-6789", "200123456789")]
    public void TryNormalize_AcceptsOldAndNewFormats(string input, string expected)
    {
        Assert.True(SriLankanNic.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
        Assert.True(SriLankanNic.IsValidNormalized(normalized));
    }

    [Theory]
    [InlineData("12345678V")]
    [InlineData("1234567890")]
    [InlineData("ABCDEFGH")]
    public void TryValidate_RejectsInvalidValues(string input)
    {
        Assert.False(SriLankanNic.TryValidate(input, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Mask_RevealsOnlyLastFourCharacters()
    {
        Assert.Equal("******789V", SriLankanNic.Mask("123456789V"));
        Assert.Equal("********6789", SriLankanNic.Mask("200123456789"));
    }
}
