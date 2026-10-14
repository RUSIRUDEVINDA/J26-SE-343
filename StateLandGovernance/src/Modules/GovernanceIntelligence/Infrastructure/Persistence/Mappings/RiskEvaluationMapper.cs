using System;
using System.Collections.Generic;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

public static class RiskEvaluationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, RiskEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        GovernanceRiskAssessmentResult result)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        var evaluationEntity = new RiskEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            SubjectId = result.SubjectId ?? string.Empty,
            OverallRiskScore = result.OverallRiskScore,
            Severity = result.Severity.ToString(),
            RequiresHumanReview = result.RequiresHumanReview,
            EvaluationTimestamp = result.EvaluationTimestamp
        };

        if (result.TriggeredIndicators != null)
        {
            for (int i = 0; i < result.TriggeredIndicators.Count; i++)
            {
                var ind = result.TriggeredIndicators[i];
                var safeActors = (ind.InvolvedActors ?? Array.Empty<string>())
                    .Where(IsSafeNonPersonalActorCode)
                    .OrderBy(a => a, StringComparer.Ordinal)
                    .ToList();

                evaluationEntity.RiskIndicators.Add(new RiskIndicatorEntity
                {
                    Id = Guid.NewGuid(),
                    RiskEvaluationId = evaluationEntity.Id,
                    IndicatorId = ind.IndicatorId ?? string.Empty,
                    Category = ind.Category.ToString(),
                    ScoreContribution = ind.ScoreContribution,
                    Severity = ind.Severity.ToString(),
                    SubjectId = ind.SubjectId ?? string.Empty,
                    InvolvedActorsJson = JsonSerializer.Serialize(safeActors),
                    TriggeredRule = ind.TriggeredRule ?? string.Empty,
                    Explanation = ind.Explanation ?? string.Empty,
                    RecommendedAction = ind.RecommendedAction ?? string.Empty,
                    OrderIndex = i
                });
            }
        }

        return (auditEntity, evaluationEntity);
    }

    private static bool IsSafeNonPersonalActorCode(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) return false;
        var trimmed = actor.Trim();

        // Omit officer/employee/user/person identifiers
        if (trimmed.StartsWith("OFF-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("OFFICER", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("EMP-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("USER-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("PERSON-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Retain explicitly safe non-personal institutional/role codes
        return trimmed.StartsWith("ROLE_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("INST-", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("DEPT-", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("AUTHORITY-", StringComparison.OrdinalIgnoreCase);
    }
}

public static class GovernanceExplanationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, GovernanceExplanationEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        GovernanceExplanationResult result)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        var evaluationEntity = new GovernanceExplanationEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            ExplanationId = result.ExplanationId ?? string.Empty,
            SubjectId = result.SubjectId ?? string.Empty,
            OverallSeverity = result.OverallSeverity.ToString(),
            RequiresHumanReview = result.RequiresHumanReview,
            Disclaimer = result.Disclaimer ?? string.Empty,
            EvaluationTimestamp = result.EvaluationTimestamp
        };

        if (result.Explanations != null)
        {
            for (int i = 0; i < result.Explanations.Count; i++)
            {
                var item = result.Explanations[i];
                evaluationEntity.Items.Add(new GovernanceExplanationItemEntity
                {
                    Id = Guid.NewGuid(),
                    GovernanceExplanationEvaluationId = evaluationEntity.Id,
                    ItemId = item.ItemId ?? string.Empty,
                    SourceEngine = item.SourceEngine.ToString(),
                    OutcomeStatus = item.OutcomeStatus ?? string.Empty,
                    Severity = item.Severity.ToString(),
                    ReasonCode = item.ReasonCode ?? string.Empty,
                    Title = item.Title ?? string.Empty,
                    PlainLanguageExplanation = item.PlainLanguageExplanation ?? string.Empty,
                    RecommendedAction = item.RecommendedAction ?? string.Empty,
                    RequiresHumanAttention = item.RequiresHumanAttention,
                    OrderIndex = i
                });
            }
        }

        return (auditEntity, evaluationEntity);
    }
}
