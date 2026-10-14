namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Tracks the state of evidence provided to support proposal compliance claims.
/// </summary>
public enum EvidenceStatus
{
    NotRequired,
    Required,
    Provided,
    Missing,
    PendingVerification,
    Verified,
    Rejected
}
