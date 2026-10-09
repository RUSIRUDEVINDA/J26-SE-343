using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service responsible for computing deterministic, tamper-evident canonical SHA-256 digests
/// for governance audit records. C# acts as the single authoritative source for canonicalization.
/// </summary>
public static class GovernanceAuditHasher
{
    public const string CanonicalFormatVersion = "AUDIT-V1";

    /// <summary>
    /// Computes the canonical SHA-256 hash (in lowercase 64-character hex) for a given governance audit record.
    /// Uses strict culture-independent invariant formatting and normalized UTC timestamp representations.
    /// </summary>
    public static string ComputeCanonicalHash(GovernanceAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var canonicalPayload = BuildCanonicalPayload(record);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Constructs the normalized canonical UTF-8 string payload for the record.
    /// Format: AUDIT-V1|{RecordId:D}|{(int)EngineType}|{NormalizedAction}|{NormalizedStatus}|{TimestampUtc:O}|{DetailsHash}
    /// </summary>
    public static string BuildCanonicalPayload(GovernanceAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var normalizedId = record.Id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
        var engineTypeInt = ((int)record.EngineType).ToString(CultureInfo.InvariantCulture);
        var normalizedAction = (record.ActionName ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedStatus = (record.Status ?? string.Empty).Trim().ToUpperInvariant();
        var utcTimestamp = record.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        var detailsText = (record.Details ?? string.Empty).Trim();
        string detailsHash;
        using (var sha256 = SHA256.Create())
        {
            var detailsBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(detailsText));
            detailsHash = Convert.ToHexString(detailsBytes).ToLowerInvariant();
        }

        return $"{CanonicalFormatVersion}|{normalizedId}|{engineTypeInt}|{normalizedAction}|{normalizedStatus}|{utcTimestamp}|{detailsHash}";
    }
}
