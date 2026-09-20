using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ConflictEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalEvaluatedDecisions { get; set; }
    public int DetectedConflictsCount { get; set; }
    public string HighestSeverity { get; set; } = string.Empty;
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<ConflictFindingEntity> Findings { get; set; } = new();
}

public class ConflictFindingEntity
{
    public Guid Id { get; set; }
    public Guid ConflictEvaluationId { get; set; }
    public string ConflictId { get; set; } = string.Empty;
    public string ConflictType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string DetectionStatus { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string InvolvedDecisionIdsJson { get; set; } = "[]";
    public string InvolvedInstitutionsJson { get; set; } = "[]";
    public string Explanation { get; set; } = string.Empty;
    public string EvidenceRule { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime DetectionTimestamp { get; set; }
    public int OrderIndex { get; set; }

    public ConflictEvaluationEntity? ConflictEvaluation { get; set; }
}
