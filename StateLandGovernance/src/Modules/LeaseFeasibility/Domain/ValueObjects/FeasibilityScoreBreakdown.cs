using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record FeasibilityScoreBreakdown
{
    public decimal IncomeToLeaseCostScore { get; }
    public decimal IncomeConsistencyScore { get; }
    public decimal DebtToIncomeScore { get; }
    public decimal EmploymentStabilityScore { get; }
    public decimal CreditIndicatorScore { get; }
    public decimal PenaltyScore { get; }
    public decimal TotalScore { get; }

    public FeasibilityScoreBreakdown(
        decimal incomeToLeaseCostScore,
        decimal incomeConsistencyScore,
        decimal debtToIncomeScore,
        decimal employmentStabilityScore,
        decimal creditIndicatorScore,
        decimal penaltyScore,
        decimal totalScore)
    {
        if (penaltyScore > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(penaltyScore), "Penalty score cannot be positive.");
        }

        if (totalScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScore), "Total score must be between 0 and 100.");
        }

        IncomeToLeaseCostScore = incomeToLeaseCostScore;
        IncomeConsistencyScore = incomeConsistencyScore;
        DebtToIncomeScore = debtToIncomeScore;
        EmploymentStabilityScore = employmentStabilityScore;
        CreditIndicatorScore = creditIndicatorScore;
        PenaltyScore = penaltyScore;
        TotalScore = totalScore;
    }
}
