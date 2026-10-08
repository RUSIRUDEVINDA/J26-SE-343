using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

public static class ConsensusEvaluationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, ConsensusEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        GovernanceConsensusResult result,
        IReadOnlyList<InstitutionalGovernancePosition> positions)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        var evaluationEntity = new ConsensusEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            ConsensusEvaluationId = result.ConsensusEvaluationId ?? string.Empty,
            SubjectId = result.SubjectId ?? string.Empty,
            Outcome = result.Outcome.ToString(),
            TotalExpectedInstitutions = result.TotalExpectedInstitutions,
            SubmittedCount = result.SubmittedCount,
            ParticipatingCount = result.ParticipatingCount,
            MissingCount = result.MissingCount,
            ApprovalCount = result.ApprovalCount,
            RejectionCount = result.RejectionCount,
            ConditionalApprovalCount = result.ConditionalApprovalCount,
            AbstentionCount = result.AbstentionCount,
            PendingCount = result.PendingCount,
            QuorumSatisfied = result.QuorumSatisfied,
            MandatoryInstitutionsSatisfied = result.MandatoryInstitutionsSatisfied,
            ConsensusThresholdSatisfied = result.ConsensusThresholdSatisfied,
            BlockingInstitutionCount = result.BlockingInstitutionCount,
            SummaryExplanation = result.SummaryExplanation ?? string.Empty,
            RecommendedAction = result.RecommendedAction ?? string.Empty,
            EvaluationTimestamp = result.EvaluationTimestamp
        };

        if (positions != null)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var pos = positions[i];
                evaluationEntity.InstitutionPositions.Add(new InstitutionPositionEntity
                {
                    Id = Guid.NewGuid(),
                    ConsensusEvaluationId = evaluationEntity.Id,
                    InstitutionId = pos.InstitutionId ?? string.Empty,
                    Position = pos.Position.ToString(),
                    AuthorityRole = pos.AuthorityRole,
                    ReasonCode = pos.ReasonCode,
                    SubmittedTimestamp = pos.SubmittedTimestamp,
                    OrderIndex = i
                });
            }
        }

        return (auditEntity, evaluationEntity);
    }
}

internal static class ConditionalVerificationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, ConditionalVerificationEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        ConditionalVerificationResult result)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        var evaluationEntity = new ConditionalVerificationEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            VerificationId = result.VerificationId ?? string.Empty,
            SubjectId = result.SubjectId ?? string.Empty,
            Outcome = result.Outcome.ToString(),
            TotalConditionsCount = result.TotalConditionsCount,
            MandatoryConditionsCount = result.MandatoryConditionsCount,
            SatisfiedMandatoryCount = result.SatisfiedMandatoryCount,
            UnsatisfiedMandatoryCount = result.UnsatisfiedMandatoryCount,
            SatisfiedOptionalCount = result.SatisfiedOptionalCount,
            PendingConditionsCount = result.PendingConditionsCount,
            MissingConditionsCount = result.MissingConditionsCount,
            ExpiredConditionsCount = result.ExpiredConditionsCount,
            SummaryExplanation = result.SummaryExplanation ?? string.Empty,
            RecommendedAction = result.RecommendedAction ?? string.Empty,
            EvaluationTimestamp = result.EvaluationTimestamp
        };

        if (result.ConditionStatuses != null)
        {
            for (int i = 0; i < result.ConditionStatuses.Count; i++)
            {
                var status = result.ConditionStatuses[i];
                evaluationEntity.ConditionResults.Add(new ConditionResultEntity
                {
                    Id = Guid.NewGuid(),
                    ConditionalVerificationEvaluationId = evaluationEntity.Id,
                    ConditionId = status.ConditionId ?? string.Empty,
                    IsMandatory = status.IsMandatory,
                    Status = status.Status.ToString(),
                    FailureReason = status.FailureReason ?? string.Empty,
                    OrderIndex = i
                });
            }
        }

        return (auditEntity, evaluationEntity);
    }
}
