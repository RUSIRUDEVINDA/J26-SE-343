using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface for early governance screening of recorded case concerns.
/// <para>
/// Boundary notes:
/// - Evaluates pre-workflow case-review indicators based purely on caller-supplied assertions.
/// - Does NOT authenticate evidence, establish legal truth, or determine applicant statutory eligibility.
/// - A finding does not constitute an automated rejection, legal finding, or personal accusation.
/// </para>
/// </summary>
public interface IEarlyGovernanceScreeningEngine
{
    /// <summary>
    /// Synchronously evaluates recorded case concerns against deterministic early governance screening rules.
    /// </summary>
    /// <param name="input">The immutable screening input snapshot.</param>
    /// <returns>The immutable screening evaluation result.</returns>
    EarlyGovernanceScreeningResult Screen(EarlyGovernanceScreeningInput input);
}
