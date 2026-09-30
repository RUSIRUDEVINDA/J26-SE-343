using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// Data transfer record for an individual early governance concern indicator in a screening request.
/// <para>
/// Note: Verified evidence states represent assertions supplied by the caller. Evidence verification and
/// authorization are outside the scope of early governance screening.
/// </para>
/// </summary>
public sealed record EarlyGovernanceIndicatorDto(
    string IndicatorType,
    string EvidenceState,
    string? EvidenceReference = null,
    DateTimeOffset? RecordedAtUtc = null
);

/// <summary>
/// Data transfer record for an individual indicator screening evaluation outcome.
/// </summary>
public sealed record EarlyGovernanceIndicatorResultDto(
    string IndicatorType,
    string EvidenceState,
    bool RequiresReview,
    bool NeedsEvidence,
    string ReasonCode,
    string Message,
    string? EvidenceReference = null,
    DateTimeOffset? RecordedAtUtc = null
);

/// <summary>
/// Data transfer record representing the overall result of early governance screening.
/// Does not represent legal approval, lease finality, or disciplinary determinations.
/// </summary>
public sealed record EarlyGovernanceScreeningResultDto(
    string CaseId,
    string InputVersion,
    string OverallStatus,
    bool HasIncompleteEvidence,
    IReadOnlyList<EarlyGovernanceIndicatorResultDto> IndicatorResults
);
