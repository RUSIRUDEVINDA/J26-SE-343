using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class NicPersonalDataProtectorTests
{
    private static readonly string TestKey =
        Convert.ToBase64String(new byte[32]);

    private static NicPersonalDataProtector CreateProtector()
    {
        Environment.SetEnvironmentVariable("LEASE_INTEREST_NIC_ENCRYPTION_KEY", TestKey);
        Environment.SetEnvironmentVariable("LEASE_INTEREST_NIC_LOOKUP_KEY", TestKey);
        return new NicPersonalDataProtector(
            Options.Create(new LeaseInterestPersonalDataOptions()));
    }

    [Fact]
    public void EncryptDecrypt_RoundTripsNormalizedNic()
    {
        var protector = CreateProtector();
        const string nic = "123456789V";

        var cipher = protector.Encrypt(nic);
        Assert.NotEqual(nic, cipher);

        var plain = protector.Decrypt(cipher);
        Assert.Equal(nic, plain);
    }

    [Fact]
    public void ComputeLookupDigest_IsStableForSameNic()
    {
        var protector = CreateProtector();
        var first = protector.ComputeLookupDigest("123456789V");
        var second = protector.ComputeLookupDigest("123456789V");
        Assert.Equal(first, second);
        Assert.NotEqual(first, protector.ComputeLookupDigest("200123456789"));
    }
}
