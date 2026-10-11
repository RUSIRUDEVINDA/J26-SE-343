namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Status resulting from comparing an off-chain governance audit record hash against the ledger anchor.
/// Note: Mismatches represent integrity divergence between off-chain and on-chain records,
/// and do not assert legal guilt, fraud, corruption, or officer misconduct.
/// </summary>
public enum AuditVerificationStatus
{
    /// <summary>
    /// Current computed off-chain hash matches the on-chain anchored hash identically.
    /// </summary>
    Match = 0,

    /// <summary>
    /// Current computed off-chain hash diverges from the on-chain anchored record hash.
    /// Indicates the off-chain record has been modified or does not match the anchored version.
    /// </summary>
    Mismatch = 1,

    /// <summary>
    /// Record anchoring is currently pending ledger inclusion; verification cannot yet be completed.
    /// </summary>
    Pending = 2,

    /// <summary>
    /// Record has not been anchored onto the blockchain ledger.
    /// </summary>
    NotAnchored = 3,

    /// <summary>
    /// Ledger verification service or network is temporarily unreachable.
    /// </summary>
    Unavailable = 4
}
