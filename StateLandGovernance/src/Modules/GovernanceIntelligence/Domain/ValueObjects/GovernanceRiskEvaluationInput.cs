using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Immutable observation record representing a historical governance decision.
/// </summary>
public sealed record DecisionHistoryObservation(
    string DecisionId,
    string SubjectId,
    string OfficerId,
    string InstitutionId,
    string DecisionType,
    DateTime Timestamp,
    bool IsOverride,
    bool IsException
);

/// <summary>
/// Immutable evidence record representing a compliance violation.
/// </summary>
public sealed record ComplianceViolationEvidence(
    string ViolationId,
    string SubjectId,
    string RuleId,
    string Category,
    DateTime Timestamp
);

/// <summary>
/// Immutable evidence record representing a detected governance conflict.
/// </summary>
public sealed record GovernanceConflictEvidence(
    string ConflictId,
    string SubjectId,
    string ConflictType,
    string Severity,
    DateTime Timestamp
);

/// <summary>
/// Immutable observation record representing a citizen or stakeholder complaint.
/// </summary>
public sealed record ComplaintObservation(
    string ComplaintId,
    string SubjectId,
    DateTime FilingTimestamp,
    string Category,
    string SeverityLevel,
    string VerificationStatus
);

/// <summary>
/// Immutable observation record representing an institutional concurrence/validation check.
/// </summary>
public sealed record InstitutionalValidationObservation(
    string ValidationId,
    string SubjectId,
    string InstitutionId,
    bool IsValidated,
    string FailureReason,
    DateTime Timestamp
);

/// <summary>
/// Aggregate input value object containing governance evidence collections for risk evaluation.
/// </summary>
public sealed record GovernanceRiskEvaluationInput(
    string SubjectId,
    IReadOnlyList<DecisionHistoryObservation> DecisionHistory,
    IReadOnlyList<ComplianceViolationEvidence> ComplianceViolations,
    IReadOnlyList<GovernanceConflictEvidence> GovernanceConflicts,
    IReadOnlyList<ComplaintObservation> Complaints,
    IReadOnlyList<InstitutionalValidationObservation> InstitutionalValidations
);
