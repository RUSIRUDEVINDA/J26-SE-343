namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Status of an off-chain governance audit record anchoring onto the blockchain trust ledger.
/// </summary>
public enum AnchorStatus
{
    /// <summary>
    /// Record has been created off-chain and queued for anchoring, awaiting block confirmation.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Record hash has been successfully anchored and confirmed on the blockchain ledger.
    /// </summary>
    Anchored = 1,

    /// <summary>
    /// Ledger submission or confirmation failed after retries.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Record exists off-chain but has not been submitted for ledger anchoring.
    /// </summary>
    NotAnchored = 3,

    /// <summary>
    /// Blockchain trust service or ledger network is temporarily unreachable.
    /// </summary>
    Unavailable = 4
}
