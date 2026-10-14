using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// DTO representing the high-level response of the conflict detection use case.
/// </summary>
public sealed record ConflictDetectionResultDto(
    string Status,
    IReadOnlyList<DetectedConflictDto> Conflicts
);
