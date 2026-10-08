namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Represents the severity bands derived from governance risk scoring.
/// </summary>
public enum GovernanceRiskSeverity
{
    Low = 1,      // Score 0 - 24
    Moderate = 2, // Score 25 - 49
    High = 3,     // Score 50 - 74
    Critical = 4  // Score 75 - 100
}
