namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Represents the assertion status of evidence supplied for an early governance indicator.
/// <para>
/// Important architectural boundary: The early governance screening engine does NOT authenticate evidence
/// or establish legal truth. Verified states are assertions supplied by the caller. Later application-layer
/// authorization and verification workflows govern who may supply or verify them.
/// </para>
/// </summary>
public enum EarlyGovernanceEvidenceState
{
    /// <summary>
    /// The supplied, verified record indicates that the concern exists.
    /// </summary>
    VerifiedPresent = 1,

    /// <summary>
    /// The supplied, verified record explicitly indicates no such concern for this assessment.
    /// </summary>
    VerifiedAbsent = 2,

    /// <summary>
    /// Information is supplied, but has not been verified.
    /// </summary>
    Unverified = 3,

    /// <summary>
    /// No evidence has been supplied for this indicator.
    /// </summary>
    Missing = 4,

    /// <summary>
    /// The evidence could not be obtained through available records.
    /// </summary>
    Unavailable = 5,

    /// <summary>
    /// An authorized assessment states that this indicator does not apply to this case.
    /// </summary>
    NotApplicable = 6
}
