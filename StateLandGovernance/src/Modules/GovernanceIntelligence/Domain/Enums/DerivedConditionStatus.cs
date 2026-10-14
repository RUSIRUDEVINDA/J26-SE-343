namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Identifies the engine-derived evaluation status for an individual governance condition.
/// </summary>
public enum DerivedConditionStatus
{
    Satisfied = 1,
    Failed = 2,
    Pending = 3,
    MissingEvidence = 4,
    Expired = 5
}
