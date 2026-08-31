namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Configurable scoring options for the Lease Feasibility Engine.
/// Live within the domain layer to maintain zero external I/O or framework dependencies.
/// </summary>
public sealed record LeaseFeasibilityScoringOptions(
    int IncomeToLeaseCostRatioWeight = 30,
    int IncomeConsistencyWeight = 20,
    int DebtToIncomeWeight = 20,
    int EmploymentStabilityWeight = 15,
    int CreditIndicatorWeight = 15
)
{
    public static LeaseFeasibilityScoringOptions Default { get; } = new();
}
