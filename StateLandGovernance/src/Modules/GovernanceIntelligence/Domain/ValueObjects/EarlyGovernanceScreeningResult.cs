using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Immutable per-indicator evaluation outcome produced by early governance screening.
/// Uses neutral, administrative phrasing and does not declare guilt, fraud, or legal finality.
/// </summary>
public sealed record EarlyGovernanceIndicatorResult
{
    public EarlyGovernanceIndicatorType IndicatorType { get; }
    public EarlyGovernanceEvidenceState EvidenceState { get; }
    public bool RequiresReview { get; }
    public bool NeedsEvidence { get; }
    public string ReasonCode { get; }
    public string Message { get; }
    public string? EvidenceReference { get; }
    public DateTimeOffset? RecordedAtUtc { get; }

    public EarlyGovernanceIndicatorResult(
        EarlyGovernanceIndicatorType indicatorType,
        EarlyGovernanceEvidenceState evidenceState,
        bool requiresReview,
        bool needsEvidence,
        string reasonCode,
        string message,
        string? evidenceReference = null,
        DateTimeOffset? recordedAtUtc = null)
    {
        IndicatorType = indicatorType;
        EvidenceState = evidenceState;
        RequiresReview = requiresReview;
        NeedsEvidence = needsEvidence;
        ReasonCode = reasonCode ?? string.Empty;
        Message = message ?? string.Empty;
        EvidenceReference = evidenceReference;
        RecordedAtUtc = recordedAtUtc;
    }
}

/// <summary>
/// Immutable overall result produced by the early governance screening engine.
/// Evaluates pre-workflow case concern indicators in a fixed, deterministic order.
/// </summary>
public sealed record EarlyGovernanceScreeningResult
{
    public string CaseId { get; }
    public string InputVersion { get; }
    public EarlyGovernanceScreeningStatus OverallStatus { get; }
    public bool HasIncompleteEvidence { get; }
    public IReadOnlyList<EarlyGovernanceIndicatorResult> IndicatorResults { get; }

    public EarlyGovernanceScreeningResult(
        string caseId,
        string inputVersion,
        EarlyGovernanceScreeningStatus overallStatus,
        bool hasIncompleteEvidence,
        IEnumerable<EarlyGovernanceIndicatorResult> indicatorResults)
    {
        CaseId = caseId ?? throw new ArgumentNullException(nameof(caseId));
        InputVersion = inputVersion ?? throw new ArgumentNullException(nameof(inputVersion));
        OverallStatus = overallStatus;
        HasIncompleteEvidence = hasIncompleteEvidence;
        IndicatorResults = (indicatorResults ?? throw new ArgumentNullException(nameof(indicatorResults)))
            .ToList()
            .AsReadOnly();
    }
}
