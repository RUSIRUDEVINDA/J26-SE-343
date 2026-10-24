using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.RealParcels;

namespace StateLandGovernance.LandIntelligence.Application.Recommendations;

/// <summary>
/// Initial state-land candidate screening from source-reported claimant category only.
/// Does not assert ownership, lease availability, or legal eligibility.
/// </summary>
public enum StateLandCandidateScreeningOutcome
{
    /// <summary>Source-reported State claimant — still not an official availability determination.</summary>
    PotentialCandidateNotVerified = 1,

    /// <summary>Claimant category missing, unchecked, unknown, unsettled, other authority, or similar.</summary>
    NeedsOfficialVerification = 2,

    /// <summary>Source-reported Private — excluded from state-land candidate results.</summary>
    ExcludedPrivate = 3,
}

public sealed record StateLandCandidateScreeningResult(
    StateLandCandidateScreeningOutcome Outcome,
    string StatusLabel,
    string Reason,
    string? ClaimantCategoryCode,
    string? ClaimantCategoryDisplay,
    string? ParcelStatusCode,
    string SourceProvenanceLabel,
    DateTimeOffset? SourceImportedAtUtc,
    bool IncludeInCandidateResults);

/// <summary>
/// Classifies parcels for state-land lease candidate screening using Survey Department/iLAS
/// source-reported claimant category (<c>pcl_if_clc</c>). Never invents lease availability.
/// </summary>
public static class SourceReportedClaimantScreening
{
    public const string PrivateCode = "3";
    public const string StateCode = "2";

    public const string PotentialCandidateLabel =
        "Potential candidate — official availability not verified";

    public const string NeedsVerificationLabel = "Needs official verification";

    public const string ExcludedPrivateLabel = "Excluded — source-reported Private";

    public static StateLandCandidateScreeningResult Evaluate(CadastralSourceMetadataDto? source)
    {
        if (source is null)
        {
            return new StateLandCandidateScreeningResult(
                StateLandCandidateScreeningOutcome.NeedsOfficialVerification,
                NeedsVerificationLabel,
                "No source-reported claimant category is available for this parcel. Official verification is required before treating it as a state-land lease candidate.",
                null,
                null,
                null,
                "Unavailable",
                null,
                IncludeInCandidateResults: true);
        }

        var code = ColomboCadastreSourceDomains.NormalizeCode(source.SourceReportedClaimantCategoryCode);
        var display = string.IsNullOrWhiteSpace(source.SourceReportedClaimantCategoryEnglishLabel)
            ? source.SourceReportedClaimantCategoryDisplay
            : source.SourceReportedClaimantCategoryEnglishLabel.Trim();

        if (string.Equals(code, PrivateCode, StringComparison.Ordinal)
            || string.Equals(display, "Private", StringComparison.OrdinalIgnoreCase))
        {
            return new StateLandCandidateScreeningResult(
                StateLandCandidateScreeningOutcome.ExcludedPrivate,
                ExcludedPrivateLabel,
                "Source-reported claimant category is Private. Excluded from state-land lease candidate results. This is a screening signal only — not a legal ownership determination.",
                string.IsNullOrWhiteSpace(code) ? null : code,
                display,
                source.SourceParcelStatusCode,
                source.SourceProvenanceLabel,
                source.ImportedAtUtc,
                IncludeInCandidateResults: false);
        }

        if (string.Equals(code, StateCode, StringComparison.Ordinal)
            || string.Equals(display, "State", StringComparison.OrdinalIgnoreCase))
        {
            return new StateLandCandidateScreeningResult(
                StateLandCandidateScreeningOutcome.PotentialCandidateNotVerified,
                PotentialCandidateLabel,
                "Source-reported claimant category is State. Included as an initial screening candidate only. Official administrative confirmation of lease availability is still required. Source parcel status is not treated as lease availability.",
                string.IsNullOrWhiteSpace(code) ? null : code,
                display,
                source.SourceParcelStatusCode,
                source.SourceProvenanceLabel,
                source.ImportedAtUtc,
                IncludeInCandidateResults: true);
        }

        var reasonCategory = string.IsNullOrWhiteSpace(display) || display == "Unavailable"
            ? "missing or unavailable"
            : $"'{display}'";

        return new StateLandCandidateScreeningResult(
            StateLandCandidateScreeningOutcome.NeedsOfficialVerification,
            NeedsVerificationLabel,
            $"Source-reported claimant category is {reasonCategory}. Included for review with mandatory official verification. Not treated as available to lease. Land use and GIS evidence do not imply eligibility.",
            string.IsNullOrWhiteSpace(code) ? null : code,
            string.IsNullOrWhiteSpace(display) ? null : display,
            source.SourceParcelStatusCode,
            source.SourceProvenanceLabel,
            source.ImportedAtUtc,
            IncludeInCandidateResults: true);
    }
}
