namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Specifies the evaluation outcome of an individual regulatory rule.
/// </summary>
public enum RuleResultStatus
{
    Compliant,
    NonCompliant,
    Conditional,
    InsufficientInformation,
    RequiresHumanReview,
    NotApplicable
}
