namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestOfficerAccessGate
{
    bool IsNicLookupEnabled { get; }

    /// <summary>
    /// Preliminary review actions (queue, status updates) — research demo pilot only.
    /// </summary>
    bool IsPreliminaryReviewEnabled { get; }

    /// <summary>
    /// Authority review queue and decisions after officer valuation — research demo pilot only.
    /// </summary>
    bool IsAuthorityReviewEnabled { get; }
}
