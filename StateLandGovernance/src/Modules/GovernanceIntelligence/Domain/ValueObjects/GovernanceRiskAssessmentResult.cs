using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing an explainable governance risk indicator.
/// </summary>
public sealed record GovernanceRiskIndicator(
    string IndicatorId,
    GovernanceRiskCategory Category,
    int ScoreContribution,
    GovernanceRiskSeverity Severity,
    string SubjectId,
    IReadOnlyList<string> InvolvedActors,
    string EvidenceSummary,
    string TriggeredRule,
    string Explanation,
    string RecommendedAction
);

/// <summary>
/// Aggregate result value object produced by the Governance Risk Engine.
/// </summary>
public sealed record GovernanceRiskAssessmentResult(
    string SubjectId,
    int OverallRiskScore,
    GovernanceRiskSeverity Severity,
    IReadOnlyList<GovernanceRiskIndicator> TriggeredIndicators,
    DateTime EvaluationTimestamp,
    bool RequiresHumanReview
);
