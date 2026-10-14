namespace StateLandGovernance.LeaseFeasibility.Domain.Enums;

/// <summary>
/// Deterministic action produced by the Component 2 scoring contract.
/// </summary>
public enum FeasibilityAction
{
    FastTrack = 1,
    Proceed = 2,
    ManualReview = 3,
    Escalate = 4,
    Reject = 5
}
