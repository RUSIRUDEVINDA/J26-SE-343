using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// Data Transfer Object for decision history observation inputs.
/// </summary>
public sealed record DecisionHistoryObservationDto(
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
/// Data Transfer Object for compliance violation evidence.
/// </summary>
public sealed record ComplianceViolationEvidenceDto(
    string ViolationId,
    string SubjectId,
    string RuleId,
    string Category,
    DateTime Timestamp
);

/// <summary>
/// Data Transfer Object for detected governance conflict evidence.
/// </summary>
public sealed record GovernanceConflictEvidenceDto(
    string ConflictId,
    string SubjectId,
    string ConflictType,
    string Severity,
    DateTime Timestamp
);

/// <summary>
/// Data Transfer Object for citizen/stakeholder complaints.
/// </summary>
public sealed record ComplaintObservationDto(
    string ComplaintId,
    string SubjectId,
    DateTime FilingTimestamp,
    string Category,
    string SeverityLevel,
    string VerificationStatus
);

/// <summary>
/// Data Transfer Object for institutional validation results.
/// </summary>
public sealed record InstitutionalValidationObservationDto(
    string ValidationId,
    string SubjectId,
    string InstitutionId,
    bool IsValidated,
    string FailureReason,
    DateTime Timestamp
);

/// <summary>
/// Data Transfer Object containing governance evidence collections for risk evaluation.
/// </summary>
public sealed record GovernanceRiskEvaluationInputDto(
    string SubjectId,
    IReadOnlyList<DecisionHistoryObservationDto> DecisionHistory,
    IReadOnlyList<ComplianceViolationEvidenceDto> ComplianceViolations,
    IReadOnlyList<GovernanceConflictEvidenceDto> GovernanceConflicts,
    IReadOnlyList<ComplaintObservationDto> Complaints,
    IReadOnlyList<InstitutionalValidationObservationDto> InstitutionalValidations
);

/// <summary>
/// Data Transfer Object representing an explainable risk indicator.
/// </summary>
public sealed record GovernanceRiskIndicatorDto(
    string IndicatorId,
    string Category,
    int ScoreContribution,
    string Severity,
    string SubjectId,
    IReadOnlyList<string> InvolvedActors,
    string EvidenceSummary,
    string TriggeredRule,
    string Explanation,
    string RecommendedAction
);

/// <summary>
/// Data Transfer Object representing the final governance risk evaluation result.
/// </summary>
public sealed record GovernanceRiskAssessmentResultDto(
    string SubjectId,
    int OverallRiskScore,
    string Severity,
    bool RequiresHumanReview,
    string Disclaimer,
    IReadOnlyList<GovernanceRiskIndicatorDto> TriggeredIndicators,
    DateTime EvaluationTimestamp
);
