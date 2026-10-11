using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class SourceReportedCadastreDisplay
{
    public static string FormatLandUseLine(CadastralSourceMetadataDto? source)
    {
        if (source is null)
        {
            return string.Empty;
        }

        var value = source.SourceReportedLandUseEnglishLabel?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = source.SourceReportedLandUseDisplay;
        }

        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"Source-reported land use: {value}";
    }

    public static string FormatClaimantCategoryLine(CadastralSourceMetadataDto? source)
    {
        if (source is null)
        {
            return string.Empty;
        }

        var value = source.SourceReportedClaimantCategoryEnglishLabel?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = source.SourceReportedClaimantCategoryDisplay;
        }

        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"Source-reported claimant category: {value}";
    }
}
