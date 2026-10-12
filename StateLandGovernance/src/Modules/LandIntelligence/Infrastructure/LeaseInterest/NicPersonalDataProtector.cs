using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class NicPersonalDataProtector : INicPersonalDataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _encryptionKey;
    private readonly byte[] _lookupKey;

    public NicPersonalDataProtector(IOptions<LeaseInterestPersonalDataOptions> options)
    {
        _encryptionKey = ResolveKey(
            Environment.GetEnvironmentVariable("LEASE_INTEREST_NIC_ENCRYPTION_KEY"),
            options.Value.EncryptionKeyBase64,
            "LEASE_INTEREST_NIC_ENCRYPTION_KEY");
        _lookupKey = ResolveKey(
            Environment.GetEnvironmentVariable("LEASE_INTEREST_NIC_LOOKUP_KEY"),
            options.Value.LookupKeyBase64,
            "LEASE_INTEREST_NIC_LOOKUP_KEY");
    }

    public string ComputeLookupDigest(string normalizedNic)
    {
        using var hmac = new HMACSHA256(_lookupKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedNic));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string Encrypt(string normalizedNic)
    {
        var plaintext = Encoding.UTF8.GetBytes(normalizedNic);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, payload, NonceSize + ciphertext.Length, TagSize);
        return Convert.ToBase64String(payload);
    }

    public string? Decrypt(string? ciphertextBase64)
    {
        if (string.IsNullOrWhiteSpace(ciphertextBase64))
        {
            return null;
        }

        var payload = Convert.FromBase64String(ciphertextBase64);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("Stored NIC ciphertext is invalid.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string Mask(string? normalizedOrPlain) => SriLankanNic.Mask(normalizedOrPlain);

    private static byte[] ResolveKey(string? environmentValue, string? configurationValue, string name)
    {
        var raw = !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : configurationValue;

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Set {name} (base64-encoded 32-byte key) or LandIntelligence:LeaseInterest:PersonalData in configuration.");
        }

        try
        {
            var bytes = Convert.FromBase64String(raw.Trim());
            if (bytes.Length != 32)
            {
                throw new InvalidOperationException($"{name} must decode to exactly 32 bytes.");
            }

            return bytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{name} must be valid base64.", ex);
        }
    }
}
