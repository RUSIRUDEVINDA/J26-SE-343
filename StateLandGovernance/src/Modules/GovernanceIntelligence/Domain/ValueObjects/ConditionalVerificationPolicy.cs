namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing policy settings for conditional governance verification.
/// </summary>
public sealed record ConditionalVerificationPolicy(
    bool AllowProvisionalVerification = true
);
