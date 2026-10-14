namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Indicates whether a regulatory rule applies to a given proposal.
/// </summary>
public enum RuleApplicability
{
    Applicable,
    NotApplicable,
    Conditional,
    Undetermined
}
