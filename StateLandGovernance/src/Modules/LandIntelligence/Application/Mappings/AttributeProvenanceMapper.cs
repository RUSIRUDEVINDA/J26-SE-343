using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class AttributeProvenanceMapper
{
    public static AttributeProvenanceDto? ToDto(AttributeProvenance? provenance) =>
        provenance is null
            ? null
            : new AttributeProvenanceDto(
                provenance.SourceType,
                provenance.SourceName,
                provenance.Confidence,
                provenance.CollectedAt,
                provenance.Verified);

    public static AttributeProvenance? ToDomain(AttributeProvenanceDto? dto) =>
        dto is null
            ? null
            : new AttributeProvenance(
                dto.SourceType,
                dto.SourceName,
                dto.Confidence,
                dto.CollectedAt,
                dto.Verified);
}
