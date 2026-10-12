using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StateLandGovernance.GovernanceIntelligence.Application.Projections;

/// <summary>
/// Closed vocabulary for Component 1 parcel ownership evidence state.
/// </summary>
public enum Component1OwnershipEvidenceStatus
{
    EvidenceProvided = 1,
    EvidenceMissing = 2,
    EvidenceConflicting = 3,
    EvidenceIncomplete = 4
}

/// <summary>
/// Closed vocabulary for Component 1 protected/sensitive zone overlap state.
/// </summary>
public enum Component1ProtectedZoneOverlapState
{
    NoProtectedZoneOverlap = 1,
    ProtectedZoneOverlapDetected = 2,
    ProtectedZoneEvidenceIncomplete = 3,
    ProtectedZoneCheckNotPerformed = 4
}

/// <summary>
/// Closed vocabulary for Component 1 survey boundary confirmation status.
/// </summary>
public enum Component1BoundaryConfirmationStatus
{
    BoundaryConfirmed = 1,
    BoundaryPendingSurvey = 2,
    BoundaryDisputed = 3,
    BoundaryEvidenceMissing = 4
}

/// <summary>
/// Immutable Component 4 read-model projection of Component 1 land/case evidence.
/// Preserves strict module boundaries without referencing Component 1 domain or application types.
/// </summary>
public sealed record Component1GovernanceCaseProjection
{
    public const int MaxIdentifierLength = 128;
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 128;
    public const int MaxCategoryLength = 128;
    private static readonly Regex Sha256HexRegex = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Component1GovernanceCaseProjection(
        Guid projectionId,
        string caseId,
        string landParcelId,
        string divisionCode,
        string districtName,
        string provinceName,
        string gnDivisionCode,
        string requestedLandUseCategory,
        decimal extentHa,
        Component1OwnershipEvidenceStatus ownershipEvidenceStatus,
        Component1ProtectedZoneOverlapState protectedZoneOverlapState,
        IReadOnlyList<string> overlappingSensitiveZoneCodes,
        Component1BoundaryConfirmationStatus boundaryConfirmationStatus,
        bool encumbranceOrDisputeFlag,
        IReadOnlyList<string> parcelEvidenceRefs,
        string inputSha256,
        GovernanceInputProvenance provenance,
        DateTimeOffset ingestedAtUtc)
    {
        if (projectionId == Guid.Empty)
        {
            throw new ArgumentException("ProjectionId cannot be empty.", nameof(projectionId));
        }

        CaseId = RequireNormalized(caseId, nameof(caseId), MaxIdentifierLength);
        LandParcelId = RequireNormalized(landParcelId, nameof(landParcelId), MaxIdentifierLength);
        DivisionCode = RequireNormalized(divisionCode, nameof(divisionCode), MaxCodeLength);
        DistrictName = RequireNormalized(districtName, nameof(districtName), MaxNameLength);
        ProvinceName = RequireNormalized(provinceName, nameof(provinceName), MaxNameLength);
        GnDivisionCode = RequireNormalized(gnDivisionCode, nameof(gnDivisionCode), MaxCodeLength);
        RequestedLandUseCategory = RequireNormalized(requestedLandUseCategory, nameof(requestedLandUseCategory), MaxCategoryLength);

        if (extentHa <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(extentHa), "ExtentHa must be strictly greater than zero.");
        }

        if (!Enum.IsDefined(ownershipEvidenceStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(ownershipEvidenceStatus), "OwnershipEvidenceStatus must be a defined value.");
        }

        if (!Enum.IsDefined(protectedZoneOverlapState))
        {
            throw new ArgumentOutOfRangeException(nameof(protectedZoneOverlapState), "ProtectedZoneOverlapState must be a defined value.");
        }

        if (!Enum.IsDefined(boundaryConfirmationStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryConfirmationStatus), "BoundaryConfirmationStatus must be a defined value.");
        }

        var normalizedZoneCodes = NormalizeStringList(overlappingSensitiveZoneCodes, nameof(overlappingSensitiveZoneCodes), allowEmpty: true);
        if (protectedZoneOverlapState == Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap &&
            normalizedZoneCodes.Count > 0)
        {
            throw new ArgumentException(
                "OverlappingSensitiveZoneCodes must be empty when ProtectedZoneOverlapState is NoProtectedZoneOverlap.",
                nameof(overlappingSensitiveZoneCodes));
        }

        if (protectedZoneOverlapState == Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected &&
            normalizedZoneCodes.Count == 0)
        {
            throw new ArgumentException(
                "OverlappingSensitiveZoneCodes must contain at least one zone code when ProtectedZoneOverlapState is ProtectedZoneOverlapDetected.",
                nameof(overlappingSensitiveZoneCodes));
        }

        var normalizedEvidenceRefs = NormalizeStringList(parcelEvidenceRefs, nameof(parcelEvidenceRefs), allowEmpty: false);

        if (string.IsNullOrWhiteSpace(inputSha256) || !Sha256HexRegex.IsMatch(inputSha256))
        {
            throw new ArgumentException("InputSha256 must be a 64-character lowercase hexadecimal SHA-256 digest.", nameof(inputSha256));
        }

        ArgumentNullException.ThrowIfNull(provenance);

        if (ingestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("IngestedAtUtc must have a zero UTC offset.", nameof(ingestedAtUtc));
        }

        ProjectionId = projectionId;
        ExtentHa = extentHa;
        OwnershipEvidenceStatus = ownershipEvidenceStatus;
        ProtectedZoneOverlapState = protectedZoneOverlapState;
        OverlappingSensitiveZoneCodes = normalizedZoneCodes;
        BoundaryConfirmationStatus = boundaryConfirmationStatus;
        EncumbranceOrDisputeFlag = encumbranceOrDisputeFlag;
        ParcelEvidenceRefs = normalizedEvidenceRefs;
        InputSha256 = inputSha256;
        Provenance = provenance;
        IngestedAtUtc = ingestedAtUtc;
    }

    public Guid ProjectionId { get; }
    public string CaseId { get; }
    public string LandParcelId { get; }
    public string DivisionCode { get; }
    public string DistrictName { get; }
    public string ProvinceName { get; }
    public string GnDivisionCode { get; }
    public string RequestedLandUseCategory { get; }
    public decimal ExtentHa { get; }
    public Component1OwnershipEvidenceStatus OwnershipEvidenceStatus { get; }
    public Component1ProtectedZoneOverlapState ProtectedZoneOverlapState { get; }
    public IReadOnlyList<string> OverlappingSensitiveZoneCodes { get; }
    public Component1BoundaryConfirmationStatus BoundaryConfirmationStatus { get; }
    public bool EncumbranceOrDisputeFlag { get; }
    public IReadOnlyList<string> ParcelEvidenceRefs { get; }
    public string InputSha256 { get; }
    public GovernanceInputProvenance Provenance { get; }
    public DateTimeOffset IngestedAtUtc { get; }

    /// <summary>
    /// Computes the deterministic canonical SHA-256 digest over all outcome-affecting Component 1 projection fields and provenance metadata.
    /// </summary>
    public static string ComputeCanonicalSha256(
        string caseId,
        string landParcelId,
        string divisionCode,
        string districtName,
        string provinceName,
        string gnDivisionCode,
        string requestedLandUseCategory,
        decimal extentHa,
        Component1OwnershipEvidenceStatus ownershipEvidenceStatus,
        Component1ProtectedZoneOverlapState protectedZoneOverlapState,
        IReadOnlyList<string> overlappingSensitiveZoneCodes,
        Component1BoundaryConfirmationStatus boundaryConfirmationStatus,
        bool encumbranceOrDisputeFlag,
        IReadOnlyList<string> parcelEvidenceRefs,
        GovernanceInputProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        var normalizedZones = NormalizeStringList(overlappingSensitiveZoneCodes, nameof(overlappingSensitiveZoneCodes), allowEmpty: true);
        var normalizedRefs = NormalizeStringList(parcelEvidenceRefs, nameof(parcelEvidenceRefs), allowEmpty: false);

        var builder = new StringBuilder();
        builder.Append("c1_projection_v1|");
        builder.Append(caseId.Trim()).Append('|');
        builder.Append(landParcelId.Trim()).Append('|');
        builder.Append(divisionCode.Trim()).Append('|');
        builder.Append(districtName.Trim()).Append('|');
        builder.Append(provinceName.Trim()).Append('|');
        builder.Append(gnDivisionCode.Trim()).Append('|');
        builder.Append(requestedLandUseCategory.Trim()).Append('|');
        builder.Append(extentHa.ToString("0.0000", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(ownershipEvidenceStatus).Append('|');
        builder.Append(protectedZoneOverlapState).Append('|');
        builder.Append(string.Join(",", normalizedZones)).Append('|');
        builder.Append(boundaryConfirmationStatus).Append('|');
        builder.Append(encumbranceOrDisputeFlag ? "1" : "0").Append('|');
        builder.Append(string.Join(",", normalizedRefs)).Append('|');
        builder.Append(provenance.SourceSystem).Append('|');
        builder.Append(provenance.SourceRecordId).Append('|');
        builder.Append(provenance.SourceRecordVersion).Append('|');
        builder.Append(provenance.IsSynthetic ? "1" : "0").Append('|');
        builder.Append(provenance.ProvenanceKind).Append('|');
        builder.Append(provenance.SyntheticSeedId ?? string.Empty).Append('|');
        builder.Append(provenance.SyntheticSeedVersion ?? string.Empty).Append('|');
        builder.Append(provenance.Purpose);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string RequireNormalized(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or whitespace.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }

    private static IReadOnlyList<string> NormalizeStringList(
        IReadOnlyList<string> items,
        string paramName,
        bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(items, paramName);

        if (!allowEmpty && items.Count == 0)
        {
            throw new ArgumentException($"{paramName} must contain at least one entry.", paramName);
        }

        var normalized = new List<string>(items.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                throw new ArgumentException($"{paramName} cannot contain null or whitespace entries.", paramName);
            }

            var trimmed = item.Trim();
            if (!seen.Add(trimmed))
            {
                throw new ArgumentException($"{paramName} cannot contain duplicate entry '{trimmed}'.", paramName);
            }

            normalized.Add(trimmed);
        }

        normalized.Sort(StringComparer.Ordinal);
        return new ReadOnlyCollection<string>(normalized);
    }
}
