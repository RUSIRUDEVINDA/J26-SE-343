namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface INicPersonalDataProtector
{
    string ComputeLookupDigest(string normalizedNic);

    string Encrypt(string normalizedNic);

    string? Decrypt(string? ciphertextBase64);

    string Mask(string? normalizedOrPlain);
}
