namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Identifies the overall outcome of a conditional governance verification evaluation.
/// </summary>
public enum ConditionalVerificationOutcome
{
    FullySatisfied = 1,
    ProvisionallySatisfied = 2,
    Unsatisfied = 3,
    PendingEvidence = 4
}
