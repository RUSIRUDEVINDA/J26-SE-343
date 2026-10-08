using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public sealed record ComplianceViolationSummaryDto(
    string RuleCode,
    string RuleCategory,
    string SummaryMessage
);

public sealed record ComplianceExplanationEvidenceDto(
    string Status,
    IReadOnlyList<ComplianceViolationSummaryDto>? ViolatedRules,
    IReadOnlyList<string>? UnsatisfiedConditions
);

public sealed record ConflictSummaryDto(
    string ConflictType,
    string Severity,
    string SummaryMessage,
    string EvidenceRuleCode,
    string RecommendedAction
);

public sealed record ConflictExplanationEvidenceDto(
    IReadOnlyList<ConflictSummaryDto>? Conflicts
);

public sealed record RiskIndicatorSummaryDto(
    string IndicatorCode,
    string Category,
    string Severity,
    string EvidenceSummary,
    string TriggeredRule,
    string RecommendedAction
);

public sealed record RiskExplanationEvidenceDto(
    int OverallScore,
    string RiskSeverity,
    bool RequiresHumanReview,
    IReadOnlyList<RiskIndicatorSummaryDto>? TriggeredIndicators
);

public sealed record GovernanceExplanationInputDto(
    string SubjectId,
    ComplianceExplanationEvidenceDto? ComplianceEvidence,
    ConflictExplanationEvidenceDto? ConflictEvidence,
    RiskExplanationEvidenceDto? RiskEvidence
);

public sealed record GovernanceExplanationItemDto(
    string ItemId,
    string SourceEngine,
    string OutcomeStatus,
    string Severity,
    string ReasonCode,
    string Title,
    string PlainLanguageExplanation,
    IReadOnlyList<string> EvidenceSummaries,
    string RecommendedAction,
    bool RequiresHumanAttention
);

public sealed record GovernanceExplanationResultDto(
    string ExplanationId,
    string SubjectId,
    string OverallSeverity,
    bool RequiresHumanReview,
    IReadOnlyList<GovernanceExplanationItemDto> Explanations,
    DateTime EvaluationTimestamp,
    string Disclaimer
);
