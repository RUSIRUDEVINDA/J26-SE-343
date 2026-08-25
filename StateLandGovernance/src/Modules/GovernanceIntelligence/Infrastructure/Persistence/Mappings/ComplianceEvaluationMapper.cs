using System;
using System.Collections.Generic;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

public static class ComplianceEvaluationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, ComplianceEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        ComplianceResult result,
        string actionName,
        string? proposalId = null)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        string? findingsJson = null;
        if (result.Findings != null && result.Findings.Count > 0)
        {
            var findingDtos = System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.Select(result.Findings, f => new ComplianceFindingDto(
                    f.RuleCode,
                    f.RuleVersion,
                    f.Category.ToString(),
                    f.Applicability.ToString(),
                    f.Status.ToString(),
                    f.Severity,
                    f.IsBlocking,
                    f.RequiresHumanReview,
                    f.ObservedValueSummary,
                    f.ExpectedRequirement,
                    f.EvidenceStatus.ToString(),
                    f.CalculationStatus.ToString(),
                    f.SourceReference?.SourceAuthority ?? string.Empty,
                    f.SourceReference?.SourceDocument ?? string.Empty,
                    f.SourceReference?.SourceSection ?? string.Empty,
                    f.RecommendedAction
                )));

            findingsJson = JsonSerializer.Serialize(findingDtos);
        }

        var evaluationEntity = new ComplianceEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            ProposalId = proposalId,
            ActionName = actionName ?? string.Empty,
            Status = result.Status.ToString(),
            DeterministicEvaluationId = result.DeterministicEvaluationId,
            RuleSetVersion = "1.0.0",
            FindingsJson = findingsJson,
            EvaluationTimestamp = auditRecord.Timestamp
        };

        return (auditEntity, evaluationEntity);
    }
}

public static class ConflictEvaluationMapper
{
    public static (GovernanceAuditRecordEntity auditEntity, ConflictEvaluationEntity evaluationEntity) MapToEntities(
        GovernanceAuditRecord auditRecord,
        IReadOnlyList<DetectedConflict> conflicts,
        string actionName,
        int totalEvaluatedDecisions)
    {
        var auditEntity = GovernanceAuditRecordMapper.ToEntity(auditRecord);

        var highestSeverity = "None";
        if (conflicts != null && conflicts.Count > 0)
        {
            if (System.Linq.Enumerable.Any(conflicts, c => string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "Critical";
            }
            else if (System.Linq.Enumerable.Any(conflicts, c => string.Equals(c.Severity, "High", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "High";
            }
            else if (System.Linq.Enumerable.Any(conflicts, c => string.Equals(c.Severity, "Medium", StringComparison.OrdinalIgnoreCase)))
            {
                highestSeverity = "Medium";
            }
            else
            {
                highestSeverity = conflicts[0].Severity ?? "Low";
            }
        }

        var evaluationEntity = new ConflictEvaluationEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditEntity.Id,
            ActionName = actionName ?? string.Empty,
            Status = (conflicts != null && conflicts.Count > 0) ? "ConflictsDetected" : "NoConflicts",
            TotalEvaluatedDecisions = totalEvaluatedDecisions,
            DetectedConflictsCount = conflicts?.Count ?? 0,
            HighestSeverity = highestSeverity,
            EvaluationTimestamp = auditRecord.Timestamp
        };

        if (conflicts != null)
        {
            for (int i = 0; i < conflicts.Count; i++)
            {
                var c = conflicts[i];
                evaluationEntity.Findings.Add(new ConflictFindingEntity
                {
                    Id = Guid.NewGuid(),
                    ConflictEvaluationId = evaluationEntity.Id,
                    ConflictId = c.ConflictId ?? string.Empty,
                    ConflictType = c.ConflictType ?? string.Empty,
                    Severity = c.Severity ?? string.Empty,
                    DetectionStatus = c.DetectionStatus ?? string.Empty,
                    SubjectId = c.SubjectId ?? string.Empty,
                    InvolvedDecisionIdsJson = JsonSerializer.Serialize(
                        (c.InvolvedDecisionIds ?? Array.Empty<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToList()),
                    InvolvedInstitutionsJson = JsonSerializer.Serialize(
                        (c.InvolvedInstitutions ?? Array.Empty<string>())
                        .Where(inst => !string.IsNullOrWhiteSpace(inst))
                        .OrderBy(inst => inst, StringComparer.Ordinal)
                        .ToList()),
                    Explanation = c.Explanation ?? string.Empty,
                    EvidenceRule = c.EvidenceRule ?? string.Empty,
                    RecommendedAction = c.RecommendedAction ?? string.Empty,
                    DetectionTimestamp = c.DetectionTimestamp,
                    OrderIndex = i
                });
            }
        }

        return (auditEntity, evaluationEntity);
    }
}
