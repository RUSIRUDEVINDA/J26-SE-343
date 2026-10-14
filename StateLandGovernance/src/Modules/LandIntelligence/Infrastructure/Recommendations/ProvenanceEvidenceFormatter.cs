using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class ProvenanceEvidenceFormatter
{
    public static string DescribeSource(AttributeProvenanceDto? provenance) =>
        provenance is null
            ? DescribeSourceType(AttributeProvenanceSourceType.Unknown)
            : DescribeSourceType(provenance.SourceType);

    public static string DescribeSourceType(AttributeProvenanceSourceType sourceType) => sourceType switch
    {
        AttributeProvenanceSourceType.Official => "Official data",
        AttributeProvenanceSourceType.ExternalAuthoritative => "External authoritative data",
        AttributeProvenanceSourceType.Derived => "Derived GIS data",
        AttributeProvenanceSourceType.Imputed => "Estimated data",
        AttributeProvenanceSourceType.Synthetic => "Synthetic data",
        _ => "Unknown data"
    };

    public static string AppendProvenance(string summary, AttributeProvenanceDto? provenance)
    {
        if (provenance is null)
        {
            return $"{summary} (Source: {DescribeSourceType(AttributeProvenanceSourceType.Unknown)}.)";
        }

        var label = DescribeSourceType(provenance.SourceType);
        if (!string.IsNullOrWhiteSpace(provenance.SourceName))
        {
            return $"{summary} (Source: {label} — {provenance.SourceName}.)";
        }

        return $"{summary} (Source: {label}.)";
    }
}
