using System.Text.Json;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

internal static class LandRecommendationPersistenceMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static LandRecommendationEntity ToPersistence(LandRecommendation recommendation) =>
        new()
        {
            Id = recommendation.Id,
            LandParcelId = recommendation.LandParcelId,
            SuitabilityScore = recommendation.SuitabilityScore,
            Rank = recommendation.Rank,
            Status = recommendation.Status,
            RecommendedUseType = recommendation.RecommendedUse?.Type,
            RecommendedUseDescription = recommendation.RecommendedUse?.Description,
            GeneratedAt = recommendation.GeneratedAt,
            CriteriaJson = JsonSerializer.Serialize(
                recommendation.Criteria.Select(c => new PersistedCriterion(
                    c.Category,
                    c.Name,
                    c.Weight,
                    c.Score,
                    c.Summary)).ToList(),
                JsonOptions),
            EvidenceJson = JsonSerializer.Serialize(
                recommendation.Evidence.Select(e => new PersistedEvidence(
                    e.Source,
                    e.Description,
                    e.RelatedCriterionName)).ToList(),
                JsonOptions)
        };

    public static LandRecommendation ToDomain(LandRecommendationEntity entity)
    {
        var recommendation = new LandRecommendation(
            entity.LandParcelId,
            entity.SuitabilityScore,
            entity.RecommendedUseType is null
                ? null
                : new LandUse(entity.RecommendedUseType.Value, entity.RecommendedUseDescription),
            entity.Status);

        PersistenceEntityIdHelper.SetEntityId(recommendation, entity.Id);

        if (entity.Rank is > 0)
        {
            recommendation.AssignRank(entity.Rank.Value);
        }

        PersistenceEntityIdHelper.SetProperty(
            recommendation,
            nameof(LandRecommendation.GeneratedAt),
            entity.GeneratedAt);

        var criteria = JsonSerializer.Deserialize<List<PersistedCriterion>>(entity.CriteriaJson, JsonOptions) ?? [];
        foreach (var criterion in criteria)
        {
            recommendation.AddCriterion(new RecommendationCriterion(
                criterion.Category,
                criterion.Name,
                criterion.Weight,
                criterion.Score,
                criterion.Summary));
        }

        var evidence = JsonSerializer.Deserialize<List<PersistedEvidence>>(entity.EvidenceJson, JsonOptions) ?? [];
        foreach (var item in evidence)
        {
            recommendation.AddEvidence(new RecommendationEvidence(
                item.Source,
                item.Description,
                item.RelatedCriterionName));
        }

        return recommendation;
    }

    private sealed record PersistedCriterion(
        CriterionCategory Category,
        string Name,
        decimal Weight,
        decimal Score,
        string? Summary);

    private sealed record PersistedEvidence(
        string Source,
        string Description,
        string? RelatedCriterionName);
}
