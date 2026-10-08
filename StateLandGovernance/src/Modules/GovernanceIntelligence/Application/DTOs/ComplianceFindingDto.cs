namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// DTO representing a detailed source-backed compliance finding for Explainable Governance decision support.
/// </summary>
public sealed record ComplianceFindingDto(
    string RuleCode,
    string RuleVersion,
    string Category,
    string Applicability,
    string Status,
    string Severity,
    bool IsBlocking,
    bool RequiresHumanReview,
    string ObservedValueSummary,
    string ExpectedRequirement,
    string EvidenceStatus,
    string CalculationStatus,
    string SourceAuthority,
    string SourceDocument,
    string SourceSection,
    string RecommendedAction
);
