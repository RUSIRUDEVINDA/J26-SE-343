using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class CriterionEvaluationFactory
{
    public static CriterionEvaluationDto Create(
        string key,
        string name,
        CriterionCategory category,
        bool isMet,
        decimal score,
        decimal weight,
        string summary,
        Domain.ValueObjects.AttributeProvenance? dataProvenance = null,
        string? attributePath = null) =>
        new(
            key,
            name,
            category,
            isMet,
            Math.Clamp(score, 0m, 100m),
            weight,
            Math.Clamp(score, 0m, 100m) * weight,
            summary,
            AttributeProvenanceMapper.ToDto(dataProvenance),
            attributePath);
}
