using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandRecommendationMapper
{
    public static LandRecommendationDto ToDto(LandRecommendation recommendation) =>
        new(
            recommendation.Id,
            recommendation.LandParcelId,
            recommendation.SuitabilityScore,
            recommendation.Rank,
            recommendation.Status,
            recommendation.RecommendedUse is null
                ? null
                : new LandUseDto(
                    recommendation.RecommendedUse.Type,
                    recommendation.RecommendedUse.Description),
            recommendation.GeneratedAt,
            recommendation.Criteria
                .Select(c => new RecommendationCriterionDto(
                    c.Category,
                    c.Name,
                    c.Weight,
                    c.Score,
                    c.Summary,
                    c.WeightedScore))
                .ToList(),
            recommendation.Evidence
                .Select(e => new RecommendationEvidenceDto(
                    e.Source,
                    e.Description,
                    e.RelatedCriterionName))
                .ToList());
}
