using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ConditionalVerificationEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string VerificationId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int TotalConditionsCount { get; set; }
    public int MandatoryConditionsCount { get; set; }
    public int SatisfiedMandatoryCount { get; set; }
    public int UnsatisfiedMandatoryCount { get; set; }
    public int SatisfiedOptionalCount { get; set; }
    public int PendingConditionsCount { get; set; }
    public int MissingConditionsCount { get; set; }
    public int ExpiredConditionsCount { get; set; }
    public string SummaryExplanation { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<ConditionResultEntity> ConditionResults { get; set; } = new();
}

public class ConditionResultEntity
{
    public Guid Id { get; set; }
    public Guid ConditionalVerificationEvaluationId { get; set; }
    public string ConditionId { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public int OrderIndex { get; set; }

    public ConditionalVerificationEvaluationEntity? ConditionalVerificationEvaluation { get; set; }
}
