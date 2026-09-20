using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Structured finding output of a regulatory rule evaluation for Explainable Governance decision support.
/// </summary>
public sealed record ComplianceFinding(
    string RuleCode,
    string RuleVersion,
    RuleEvaluationType Category,
    RuleApplicability Applicability,
    RuleResultStatus Status,
    string Severity,
    bool IsBlocking,
    bool RequiresHumanReview,
    string ObservedValueSummary,
    string ExpectedRequirement,
    EvidenceStatus EvidenceStatus,
    CalculationStatus CalculationStatus,
    RuleSourceMetadata SourceReference,
    string RecommendedAction
);
