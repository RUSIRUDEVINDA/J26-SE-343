namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing input parameters for lease regulatory compliance evaluation.
/// </summary>
public sealed record LeaseEvaluationInput(
    int LeaseDurationYears,
    string ProposedUse,
    decimal LeaseAmount,
    string ZoningArea
);
