using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class RiskEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public int OverallRiskScore { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<RiskIndicatorEntity> RiskIndicators { get; set; } = new();
}

public class RiskIndicatorEntity
{
    public Guid Id { get; set; }
    public Guid RiskEvaluationId { get; set; }
    public string IndicatorId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ScoreContribution { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string InvolvedActorsJson { get; set; } = "[]";
    public string TriggeredRule { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public int OrderIndex { get; set; }

    public RiskEvaluationEntity? RiskEvaluation { get; set; }
}
