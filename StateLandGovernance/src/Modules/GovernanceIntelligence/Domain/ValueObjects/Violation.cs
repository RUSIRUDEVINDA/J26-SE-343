namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing a regulatory rule violation.
/// </summary>
public sealed record Violation(
    string RuleCode,
    string Message
);
