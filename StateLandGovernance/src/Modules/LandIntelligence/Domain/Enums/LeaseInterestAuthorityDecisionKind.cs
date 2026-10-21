namespace StateLandGovernance.LandIntelligence.Domain.Enums;

/// <summary>
/// Authority reviewer decision after an officer valuation. Exactly two outcomes.
/// Not a lease issuance, land reservation, or official government lease approval.
/// </summary>
public enum LeaseInterestAuthorityDecisionKind
{
    Approved = 1,
    Rejected = 2,
}
