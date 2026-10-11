namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Safe, source-reported cadastral metadata for real imported parcels.
/// Excludes claimant names, ownership assertions, operator fields, and legal suitability signals.
/// </summary>
public sealed record CadastralSourceMetadataDto(
    string SourceProvenanceLabel,
    string SourceLayer,
    string SourceFeatureId,
    string? SourceReportedLandUseCode,
    string? SourceReportedLandUseEnglishLabel,
    string? SourceReportedLandUseText,
    string? SourceReportedClaimantCategoryCode,
    string? SourceReportedClaimantCategoryEnglishLabel,
    string? SourceParcelStatusCode,
    string? GnIndex,
    string? CoordinateStatus,
    DateTimeOffset ImportedAtUtc)
{
    /// <summary>Human-readable land use for UI when English label or unknown-code fallback is applied.</summary>
    public string SourceReportedLandUseDisplay { get; init; } = string.Empty;

    /// <summary>Human-readable claimant category for UI (not legal ownership).</summary>
    public string SourceReportedClaimantCategoryDisplay { get; init; } = string.Empty;

    /// <summary>Research dataset join key when imported from the Colombo state research 300 export.</summary>
    public string? ResearchRecordId { get; init; }

    /// <summary>Representative map pin from the research export (not the stored parcel centroid).</summary>
    public double? MapMarkerLatitude { get; init; }

    public double? MapMarkerLongitude { get; init; }
}

public sealed record SourceReportedLandUseOptionDto(
    string Code,
    string EnglishLabel);
