namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Status of a financial or economic appraisal recalculation.
/// </summary>
public enum CalculationStatus
{
    Calculated,
    RequiresStakeholderConfirmation,
    NonConvergent,
    MultipleRootsDetected,
    NoRootFound,
    UnsupportedInterval,
    MissingInputs
}
