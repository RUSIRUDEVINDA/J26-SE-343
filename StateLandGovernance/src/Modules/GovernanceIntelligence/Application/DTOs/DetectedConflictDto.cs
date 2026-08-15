using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// DTO representing a detected conflict outcome.
/// </summary>
public sealed record DetectedConflictDto(
    string ConflictId,
    string ConflictType,
    string Severity,
    string DetectionStatus,
    IReadOnlyList<string> InvolvedDecisionIds,
    IReadOnlyList<string> InvolvedInstitutions,
    string SubjectId,
    string Explanation,
    string EvidenceRule,
    string RecommendedAction,
    DateTime DetectionTimestamp
);
