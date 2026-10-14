using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing a detected regulatory or jurisdictional conflict.
/// </summary>
public sealed record DetectedConflict
{
    public string ConflictId { get; init; }
    public string ConflictType { get; init; }
    public string Severity { get; init; }
    public string DetectionStatus { get; init; }
    public IReadOnlyList<string> InvolvedDecisionIds { get; init; }
    public IReadOnlyList<string> InvolvedInstitutions { get; init; }
    public string SubjectId { get; init; }
    public string Explanation { get; init; }
    public string EvidenceRule { get; init; }
    public string RecommendedAction { get; init; }
    public DateTime DetectionTimestamp { get; init; }

    public DetectedConflict(
        string conflictId,
        string conflictType,
        string severity,
        string detectionStatus,
        IReadOnlyList<string> involvedDecisionIds,
        IReadOnlyList<string> involvedInstitutions,
        string subjectId,
        string explanation,
        string evidenceRule,
        string recommendedAction,
        DateTime detectionTimestamp)
    {
        ConflictId = conflictId;
        ConflictType = conflictType;
        Severity = severity;
        DetectionStatus = detectionStatus;
        InvolvedDecisionIds = involvedDecisionIds != null 
            ? involvedDecisionIds.ToList().AsReadOnly() 
            : Array.Empty<string>();
        InvolvedInstitutions = involvedInstitutions != null 
            ? involvedInstitutions.ToList().AsReadOnly() 
            : Array.Empty<string>();
        SubjectId = subjectId;
        Explanation = explanation;
        EvidenceRule = evidenceRule;
        RecommendedAction = recommendedAction;
        DetectionTimestamp = detectionTimestamp;
    }
}
