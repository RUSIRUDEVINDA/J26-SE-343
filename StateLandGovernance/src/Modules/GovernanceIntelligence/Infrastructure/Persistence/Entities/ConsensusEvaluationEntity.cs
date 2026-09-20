using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ConsensusEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string ConsensusEvaluationId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int TotalExpectedInstitutions { get; set; }
    public int SubmittedCount { get; set; }
    public int ParticipatingCount { get; set; }
    public int MissingCount { get; set; }
    public int ApprovalCount { get; set; }
    public int RejectionCount { get; set; }
    public int ConditionalApprovalCount { get; set; }
    public int AbstentionCount { get; set; }
    public int PendingCount { get; set; }
    public bool QuorumSatisfied { get; set; }
    public bool MandatoryInstitutionsSatisfied { get; set; }
    public bool ConsensusThresholdSatisfied { get; set; }
    public int BlockingInstitutionCount { get; set; }
    public string SummaryExplanation { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<InstitutionPositionEntity> InstitutionPositions { get; set; } = new();
}

public class InstitutionPositionEntity
{
    public Guid Id { get; set; }
    public Guid ConsensusEvaluationId { get; set; }
    public string InstitutionId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? AuthorityRole { get; set; }
    public string? ReasonCode { get; set; }
    public DateTime? SubmittedTimestamp { get; set; }
    public int OrderIndex { get; set; }

    public ConsensusEvaluationEntity? ConsensusEvaluation { get; set; }
}
