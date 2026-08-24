using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object holding the output results of a regulatory compliance evaluation.
/// </summary>
public sealed class ComplianceResult
{
    public ComplianceStatus Status { get; }
    public IReadOnlyList<ComplianceFinding> Findings { get; }
    public IReadOnlyList<Violation> Violations { get; }
    public IReadOnlyList<ComplianceCondition> Conditions { get; }
    public string DeterministicEvaluationId { get; }
    public DateTime EvaluationTimestamp { get; }

    public ComplianceResult(
        ComplianceStatus status,
        IReadOnlyList<Violation> violations,
        IReadOnlyList<ComplianceCondition> conditions)
        : this(status, Array.Empty<ComplianceFinding>(), string.Empty, DateTime.UtcNow, violations, conditions)
    {
    }

    public ComplianceResult(
        ComplianceStatus status,
        IReadOnlyList<ComplianceFinding> findings,
        string deterministicEvaluationId,
        DateTime evaluationTimestamp,
        IReadOnlyList<Violation> violations = null,
        IReadOnlyList<ComplianceCondition> conditions = null)
    {
        Status = status;
        Findings = findings ?? Array.Empty<ComplianceFinding>();
        DeterministicEvaluationId = deterministicEvaluationId ?? string.Empty;
        EvaluationTimestamp = evaluationTimestamp;

        // Populate backward-compatible Violations & Conditions collections if not provided
        if (violations != null)
        {
            Violations = violations;
        }
        else
        {
            Violations = Findings
                .Where(f => f.Status == RuleResultStatus.NonCompliant)
                .Select(f => new Violation(f.RuleCode, $"{f.ObservedValueSummary} - Recommended Action: {f.RecommendedAction}"))
                .ToList();
        }

        if (conditions != null)
        {
            Conditions = conditions;
        }
        else
        {
            Conditions = Findings
                .Where(f => f.Status == RuleResultStatus.Conditional || f.Status == RuleResultStatus.RequiresHumanReview || f.Status == RuleResultStatus.InsufficientInformation)
                .Select(f => new ComplianceCondition($"{f.RuleCode}: {f.ObservedValueSummary} ({f.RecommendedAction})", null))
                .ToList();
        }
    }
}
