using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Aggregate result value object produced by the Explainable Governance Engine.
/// </summary>
public sealed record GovernanceExplanationResult
{
    public string ExplanationId { get; }
    public string SubjectId { get; }
    public GovernanceExplanationSeverity OverallSeverity { get; }
    public bool RequiresHumanReview { get; }
    public IReadOnlyList<GovernanceExplanationItem> Explanations { get; }
    public DateTime EvaluationTimestamp { get; }
    public string Disclaimer { get; }

    public GovernanceExplanationResult(
        string explanationId,
        string subjectId,
        GovernanceExplanationSeverity overallSeverity,
        bool requiresHumanReview,
        IEnumerable<GovernanceExplanationItem>? explanations,
        DateTime evaluationTimestamp,
        string disclaimer)
    {
        ExplanationId = explanationId ?? string.Empty;
        SubjectId = subjectId ?? string.Empty;
        OverallSeverity = overallSeverity;
        RequiresHumanReview = requiresHumanReview;
        Explanations = (explanations ?? Array.Empty<GovernanceExplanationItem>()).Where(e => e != null).ToList().AsReadOnly();
        EvaluationTimestamp = evaluationTimestamp;
        Disclaimer = disclaimer ?? string.Empty;
    }
}
