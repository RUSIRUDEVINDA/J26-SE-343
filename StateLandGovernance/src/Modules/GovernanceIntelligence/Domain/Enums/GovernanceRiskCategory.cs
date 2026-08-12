namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Identifies the governance risk categories evaluated by Phase 4.
/// </summary>
public enum GovernanceRiskCategory
{
    ApprovalPatternAnomaly = 1,
    RepeatedOverrideRisk = 2,
    RegulatoryViolationPattern = 3,
    ConflictRecurrenceRisk = 4,
    ComplaintRiskIndicator = 5,
    InstitutionalValidationRisk = 6,
    TemporalActivityAnomaly = 7,
    DecisionConcentrationRisk = 8,
    ExceptionFrequencyRisk = 9,
    CrossEvidenceRisk = 10
}
