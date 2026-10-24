using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Recommendations;

/// <summary>
/// Scoring-only mapping from Survey Department/iLAS source-reported land-use codes
/// to recommendation purpose alignment. Does not write domain LandUseType or ownership fields.
/// </summary>
public static class SourceReportedPurposeAlignment
{
    public enum AlignmentBand
    {
        /// <summary>Clear positive match for the requested purpose.</summary>
        StrongMatch,
        /// <summary>Related agricultural/residential signal without claiming identity.</summary>
        RelatedMatch,
        /// <summary>Known label that is not a positive purpose match (neutral).</summary>
        Neutral,
        /// <summary>Access, road, water/hydro, or similar — never a fabricated positive match.</summary>
        InfrastructureOrHydro,
        /// <summary>No usable source-reported land use.</summary>
        Unavailable
    }

    // Official domain codes: cultivation / garden-type agricultural signals.
    private static readonly HashSet<int> AgriculturalStrong =
    [
        73, // Paddy Field
        71, // Chena
        72, // Ovita
        74, // Tea
        75, // Coconut
        76, // Rubber
        77, // Cinnamon
        78, // Coffee
        79, // Cocoa
        80, // Tobacco
        89  // Other Cultivation
    ];

    private static readonly HashSet<int> AgriculturalRelated =
    [
        2,  // Garden
        3,  // Deniya
        70  // Estate
    ];

    // Homegarden / house-related residential signals only.
    private static readonly HashSet<int> ResidentialStrong =
    [
        1 // Homegarden
    ];

    // No clear commercial codes exist in the Colombo cadastre land-use domain.
    // Do not fabricate commercial matches from Estate/Quarry/etc.
    private static readonly HashSet<int> CommercialStrong = [];

    private static readonly HashSet<int> AccessOrRoad =
    [
        50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 69
    ];

    private static readonly HashSet<int> WaterOrHydro =
    [
        14, 15, 16, 17, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 39
    ];

    public static AlignmentBand Classify(
        LandUseType requiredPurpose,
        string? sourceLandUseCode,
        string? sourceLandUseEnglishLabel)
    {
        if (!TryResolveCode(sourceLandUseCode, sourceLandUseEnglishLabel, out var code))
        {
            return AlignmentBand.Unavailable;
        }

        if (AccessOrRoad.Contains(code) || WaterOrHydro.Contains(code))
        {
            return AlignmentBand.InfrastructureOrHydro;
        }

        return requiredPurpose switch
        {
            LandUseType.Agricultural when AgriculturalStrong.Contains(code) => AlignmentBand.StrongMatch,
            LandUseType.Agricultural when AgriculturalRelated.Contains(code) => AlignmentBand.RelatedMatch,
            LandUseType.Residential when ResidentialStrong.Contains(code) => AlignmentBand.StrongMatch,
            LandUseType.Commercial when CommercialStrong.Contains(code) => AlignmentBand.StrongMatch,
            LandUseType.Commercial when MatchesCommercialLabel(sourceLandUseEnglishLabel) => AlignmentBand.StrongMatch,
            _ => AlignmentBand.Neutral
        };
    }

    public static (decimal Score, bool IsMet, string Summary) ToEvaluation(
        LandUseType requiredPurpose,
        AlignmentBand band,
        string? sourceDisplay)
    {
        var label = string.IsNullOrWhiteSpace(sourceDisplay) ? "unavailable" : sourceDisplay.Trim();
        return band switch
        {
            AlignmentBand.StrongMatch => (
                100m,
                true,
                $"Source-reported land use ({label}) aligns with requested purpose ({requiredPurpose}) — scoring signal only, not legal use or ownership."),
            AlignmentBand.RelatedMatch => (
                70m,
                true,
                $"Source-reported land use ({label}) is related to requested purpose ({requiredPurpose}) — scoring signal only."),
            AlignmentBand.InfrastructureOrHydro => (
                15m,
                false,
                $"Source-reported land use ({label}) is access/road/water infrastructure and is not treated as a positive match for purpose ({requiredPurpose})."),
            AlignmentBand.Unavailable => (
                30m,
                false,
                $"Source-reported land use is unavailable; purpose alignment with ({requiredPurpose}) was not positively assessed."),
            _ => (
                30m,
                false,
                $"Source-reported land use ({label}) has limited alignment with requested purpose ({requiredPurpose}).")
        };
    }

    private static bool TryResolveCode(string? code, string? englishLabel, out int resolved)
    {
        if (ColomboCadastreSourceDomains.TryParseLandUseCode(code, out resolved))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(englishLabel))
        {
            var trimmed = englishLabel.Trim();
            foreach (var (optionCode, optionLabel) in ColomboCadastreSourceDomains.ListLandUseFilterOptions())
            {
                if (string.Equals(optionLabel, trimmed, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(optionCode, out resolved))
                {
                    return true;
                }
            }
        }

        resolved = 0;
        return false;
    }

    private static bool MatchesCommercialLabel(string? englishLabel)
    {
        if (string.IsNullOrWhiteSpace(englishLabel))
        {
            return false;
        }

        var label = englishLabel.Trim();
        return label.Contains("commercial", StringComparison.OrdinalIgnoreCase)
               || label.Contains("shop", StringComparison.OrdinalIgnoreCase)
               || label.Contains("market", StringComparison.OrdinalIgnoreCase)
               || label.Contains("retail", StringComparison.OrdinalIgnoreCase);
    }
}
