using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record FeasibilityScoreBreakdown
{
    public decimal DebtServiceRatio { get; }
    public decimal LiquidityBufferMonths { get; }
    public decimal DebtServiceRatioScore { get; }
    public decimal IncomeConsistencyScore { get; }
    public decimal LiquidityBufferScore { get; }
    public decimal CreditHistoryScore { get; }
    public decimal PenaltyScore { get; }
    public decimal TotalScore { get; }

    public FeasibilityScoreBreakdown(
        decimal debtServiceRatio,
        decimal liquidityBufferMonths,
        decimal debtServiceRatioScore,
        decimal incomeConsistencyScore,
        decimal liquidityBufferScore,
        decimal creditHistoryScore,
        decimal penaltyScore,
        decimal totalScore)
    {
        if (debtServiceRatio < 0m) throw new ArgumentOutOfRangeException(nameof(debtServiceRatio));
        if (liquidityBufferMonths < 0m) throw new ArgumentOutOfRangeException(nameof(liquidityBufferMonths));
        ValidateContribution(debtServiceRatioScore, nameof(debtServiceRatioScore));
        ValidateContribution(incomeConsistencyScore, nameof(incomeConsistencyScore));
        ValidateContribution(liquidityBufferScore, nameof(liquidityBufferScore));
        ValidateContribution(creditHistoryScore, nameof(creditHistoryScore));

        if (penaltyScore > 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(penaltyScore), "Penalty score cannot be positive.");
        }

        if (totalScore is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalScore), "Total score must be between 0 and 100.");
        }

        DebtServiceRatio = debtServiceRatio;
        LiquidityBufferMonths = liquidityBufferMonths;
        DebtServiceRatioScore = debtServiceRatioScore;
        IncomeConsistencyScore = incomeConsistencyScore;
        LiquidityBufferScore = liquidityBufferScore;
        CreditHistoryScore = creditHistoryScore;
        PenaltyScore = penaltyScore;
        TotalScore = totalScore;
    }

    private static void ValidateContribution(decimal score, string parameterName)
    {
        if (score < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Positive score contributions cannot be negative.");
        }
    }
}
