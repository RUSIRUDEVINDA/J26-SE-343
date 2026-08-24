using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class GovernanceExplanationEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string ExplanationId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string OverallSeverity { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<GovernanceExplanationItemEntity> Items { get; set; } = new();
}

public class GovernanceExplanationItemEntity
{
    public Guid Id { get; set; }
    public Guid GovernanceExplanationEvaluationId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string SourceEngine { get; set; } = string.Empty;
    public string OutcomeStatus { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PlainLanguageExplanation { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public bool RequiresHumanAttention { get; set; }
    public int OrderIndex { get; set; }

    public GovernanceExplanationEvaluationEntity? ExplanationEvaluation { get; set; }
}
