using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record AttributeProvenanceDto(
    AttributeProvenanceSourceType SourceType,
    string? SourceName,
    decimal? Confidence,
    DateTimeOffset? CollectedAt,
    bool Verified);
