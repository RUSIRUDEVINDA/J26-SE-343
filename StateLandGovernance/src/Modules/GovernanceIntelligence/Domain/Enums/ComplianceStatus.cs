namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Represents the result status of a compliance evaluation.
/// </summary>
public enum ComplianceStatus
{
    Compliant = 1,
    NonCompliant = 2,
    Conditional = 3,
    RequiresReview = 4
}
