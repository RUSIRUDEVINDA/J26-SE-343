namespace StateLandGovernance.LandIntelligence.Application.Configuration;

/// <summary>
/// Keys for encrypting applicant NIC and computing lookup digests. Set via environment variables
/// or configuration — never commit real key material.
/// </summary>
public sealed class LeaseInterestPersonalDataOptions
{
    public const string SectionName = "LandIntelligence:LeaseInterest:PersonalData";

    /// <summary>Base64-encoded 32-byte AES key (LEASE_INTEREST_NIC_ENCRYPTION_KEY).</summary>
    public string? EncryptionKeyBase64 { get; set; }

    /// <summary>Base64-encoded 32-byte HMAC key (LEASE_INTEREST_NIC_LOOKUP_KEY).</summary>
    public string? LookupKeyBase64 { get; set; }
}
