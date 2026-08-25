using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public class ComplianceEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string? ProposalId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeterministicEvaluationId { get; set; }
    public string RuleSetVersion { get; set; } = "1.0.0";
    public string? FindingsJson { get; set; }
    public DateTime EvaluationTimestamp { get; set; }

    public GovernanceAuditRecordEntity? AuditRecord { get; set; }
}
