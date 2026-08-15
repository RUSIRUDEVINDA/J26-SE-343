using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface to evaluate governance decision snapshots and flag conflicts.
/// </summary>
public interface IGovernanceConflictEngine
{
    /// <summary>
    /// Deterministically checks the provided decisions and returns a list of detected conflicts.
    /// </summary>
    IReadOnlyList<DetectedConflict> DetectConflicts(IReadOnlyList<GovernanceDecisionSnapshot> decisions, DateTime evaluationTimestamp);
}
