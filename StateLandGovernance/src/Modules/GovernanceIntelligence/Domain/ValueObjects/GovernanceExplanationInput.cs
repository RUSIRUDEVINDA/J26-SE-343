using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Phase 5-owned summarized evidence contract for regulatory compliance outcomes.
/// </summary>
public sealed record ComplianceExplanationEvidence
{
    public string Status { get; }
    public IReadOnlyList<ComplianceViolationSummary> ViolatedRules { get; }
    public IReadOnlyList<string> UnsatisfiedConditions { get; }

    public ComplianceExplanationEvidence(
        string status,
        IEnumerable<ComplianceViolationSummary>? violatedRules,
        IEnumerable<string>? unsatisfiedConditions)
    {
        Status = status ?? string.Empty;
        ViolatedRules = (violatedRules ?? Array.Empty<ComplianceViolationSummary>()).Where(v => v != null).ToList().AsReadOnly();
        UnsatisfiedConditions = (unsatisfiedConditions ?? Array.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList().AsReadOnly();
    }
}

/// <summary>
/// Phase 5-owned summarized evidence item for a compliance violation.
/// </summary>
public sealed record ComplianceViolationSummary(
    string RuleCode,
    string RuleCategory,
    string SummaryMessage
);

/// <summary>
/// Phase 5-owned summarized evidence contract for governance conflict outcomes.
/// </summary>
public sealed record ConflictExplanationEvidence
{
    public IReadOnlyList<ConflictSummary> Conflicts { get; }

    public ConflictExplanationEvidence(IEnumerable<ConflictSummary>? conflicts)
    {
        Conflicts = (conflicts ?? Array.Empty<ConflictSummary>()).Where(c => c != null).ToList().AsReadOnly();
    }
}

/// <summary>
/// Phase 5-owned summarized evidence item for a detected conflict.
/// </summary>
public sealed record ConflictSummary(
    string ConflictType,
    GovernanceExplanationSeverity Severity,
    string SummaryMessage,
    string EvidenceRuleCode,
    string RecommendedAction
);

/// <summary>
/// Phase 5-owned summarized evidence contract for governance risk outcomes.
/// </summary>
public sealed record RiskExplanationEvidence
{
    public int OverallScore { get; }
    public GovernanceExplanationSeverity RiskSeverity { get; }
    public bool RequiresHumanReview { get; }
    public IReadOnlyList<RiskIndicatorSummary> TriggeredIndicators { get; }

    public RiskExplanationEvidence(
        int overallScore,
        GovernanceExplanationSeverity riskSeverity,
        bool requiresHumanReview,
        IEnumerable<RiskIndicatorSummary>? triggeredIndicators)
    {
        OverallScore = overallScore;
        RiskSeverity = riskSeverity;
        RequiresHumanReview = requiresHumanReview;
        TriggeredIndicators = (triggeredIndicators ?? Array.Empty<RiskIndicatorSummary>()).Where(i => i != null).ToList().AsReadOnly();
    }
}

/// <summary>
/// Phase 5-owned summarized evidence item for a triggered risk indicator.
/// </summary>
public sealed record RiskIndicatorSummary(
    string IndicatorCode,
    string Category,
    GovernanceExplanationSeverity Severity,
    string EvidenceSummary,
    string TriggeredRule,
    string RecommendedAction
);

/// <summary>
/// Aggregate domain input contract for the Explainable Governance Engine.
/// </summary>
public sealed record GovernanceExplanationInput
{
    public string SubjectId { get; }
    public ComplianceExplanationEvidence? ComplianceEvidence { get; }
    public ConflictExplanationEvidence? ConflictEvidence { get; }
    public RiskExplanationEvidence? RiskEvidence { get; }

    public GovernanceExplanationInput(
        string subjectId,
        ComplianceExplanationEvidence? complianceEvidence,
        ConflictExplanationEvidence? conflictEvidence,
        RiskExplanationEvidence? riskEvidence)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("Subject ID cannot be null or empty.", nameof(subjectId));
        }

        SubjectId = subjectId.Trim();
        ComplianceEvidence = complianceEvidence;
        ConflictEvidence = conflictEvidence;
        RiskEvidence = riskEvidence;
    }
}
