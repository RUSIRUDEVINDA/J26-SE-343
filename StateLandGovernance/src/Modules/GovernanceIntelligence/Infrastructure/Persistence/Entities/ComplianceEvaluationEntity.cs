using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ComplianceEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
    public List<ComplianceViolationEntity> Violations { get; set; } = new();
    public List<ComplianceConditionEntity> Conditions { get; set; } = new();
}

public class ComplianceViolationEntity
{
    public Guid Id { get; set; }
    public Guid ComplianceEvaluationId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int OrderIndex { get; set; }

    public ComplianceEvaluationEntity? ComplianceEvaluation { get; set; }
}

public class ComplianceConditionEntity
{
    public Guid Id { get; set; }
    public Guid ComplianceEvaluationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? RequiredByDate { get; set; }
    public int OrderIndex { get; set; }

    public ComplianceEvaluationEntity? ComplianceEvaluation { get; set; }
}
