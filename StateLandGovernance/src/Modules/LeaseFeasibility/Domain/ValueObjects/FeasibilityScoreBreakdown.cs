using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record FeasibilityScoreBreakdown
{
    public decimal DebtToIncomeScore { get; }
    public decimal IncomeConsistencyScore { get; }
    public decimal LiquidityBufferScore { get; }
    public decimal CreditHistoryScore { get; }
    public decimal PenaltyScore { get; }
    public decimal TotalScore { get; }

    public FeasibilityScoreBreakdown(
        decimal debtToIncomeScore,
        decimal incomeConsistencyScore,
        decimal liquidityBufferScore,
        decimal creditHistoryScore,
        decimal penaltyScore,
        decimal totalScore)
    {
        if (debtToIncomeScore is < 0 or > 35)
        {
            throw new ArgumentOutOfRangeException(nameof(debtToIncomeScore), "Debt-to-Income score must be between 0 and 35.");
        }

        if (incomeConsistencyScore is < 0 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(incomeConsistencyScore), "Income Consistency score must be between 0 and 25.");
        }

        if (liquidityBufferScore is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(liquidityBufferScore), "Liquidity Buffer score must be between 0 and 20.");
        }

        if (creditHistoryScore is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(creditHistoryScore), "Credit History score must be between 0 and 20.");
        }

        if (penaltyScore > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(penaltyScore), "Penalty score cannot be positive.");
        }

        if (totalScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScore), "Total score must be between 0 and 100.");
        }

        DebtToIncomeScore = debtToIncomeScore;
        IncomeConsistencyScore = incomeConsistencyScore;
        LiquidityBufferScore = liquidityBufferScore;
        CreditHistoryScore = creditHistoryScore;
        PenaltyScore = penaltyScore;
        TotalScore = totalScore;
    }
}
