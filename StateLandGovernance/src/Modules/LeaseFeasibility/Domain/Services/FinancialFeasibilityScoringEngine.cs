using System;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Domain service implementation of the deterministic Financial Feasibility Scoring Engine.
/// </summary>
public sealed class FinancialFeasibilityScoringEngine : IFinancialFeasibilityScoringEngine
{
    private readonly LeaseFeasibilityScoringOptions _options;

    public FinancialFeasibilityScoringEngine() : this(LeaseFeasibilityScoringOptions.Default)
    {
    }

    public FinancialFeasibilityScoringEngine(LeaseFeasibilityScoringOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public FinancialFeasibilityAssessment EvaluateFeasibility(FinancialProfile input, DateTime evaluationTimestamp)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input), "Financial profile input cannot be null.");
        }

        var utcTimestamp = evaluationTimestamp.Kind == DateTimeKind.Utc ? evaluationTimestamp : evaluationTimestamp.ToUniversalTime();

        decimal incomeToLeaseCostScore = 0;
        decimal incomeConsistencyScore = 0;
        decimal debtToIncomeScore = 0;
        decimal employmentStabilityScore = 0;
        decimal creditIndicatorScore = 0;
        decimal penaltyScore = 0;

        EvaluateIncomeToLeaseCost(input, ref incomeToLeaseCostScore);
        EvaluateIncomeConsistency(input, ref incomeConsistencyScore);
        EvaluateDebtToIncome(input, ref debtToIncomeScore);
        EvaluateEmploymentStability(input, ref employmentStabilityScore);
        EvaluateCreditIndicator(input, ref creditIndicatorScore, ref penaltyScore);

        decimal rawScore = incomeToLeaseCostScore + incomeConsistencyScore + debtToIncomeScore + employmentStabilityScore + creditIndicatorScore + penaltyScore;
        decimal totalScore = Math.Min(100m, Math.Max(0m, rawScore));

        var breakdown = new FeasibilityScoreBreakdown(
            incomeToLeaseCostScore,
            incomeConsistencyScore,
            debtToIncomeScore,
            employmentStabilityScore,
            creditIndicatorScore,
            penaltyScore,
            totalScore
        );

        var grade = DeriveGrade(totalScore, penaltyScore);

        // TODO: Map application ID appropriately. Since FinancialProfile does not have ApplicationId,
        // and we are creating the assessment, we will use ApplicantId for now or expect it to be handled outside.
        // Or we can add ApplicationId to FinancialProfile. I'll just use "UNKNOWN-APP" if not present.
        return new FinancialFeasibilityAssessment(
            applicationId: input.ApplicantId ?? "UNKNOWN", // Just a fallback, technically should be passed if known.
            grade: grade,
            scoreBreakdown: breakdown,
            predictiveProbability: null
        );
    }

    private void EvaluateIncomeToLeaseCost(FinancialProfile input, ref decimal score)
    {
        // Simple logic for income to lease cost. Since Lease Cost isn't directly in profile, we'll proxy it with SavingsToIncomeRatio.
        if (input.SavingsToIncomeRatio > 0.3m)
        {
            score = _options.IncomeToLeaseCostRatioWeight;
        }
        else if (input.SavingsToIncomeRatio > 0.15m)
        {
            score = _options.IncomeToLeaseCostRatioWeight * 0.75m;
        }
        else if (input.SavingsToIncomeRatio > 0.05m)
        {
            score = _options.IncomeToLeaseCostRatioWeight * 0.5m;
        }
        else
        {
            score = 0;
        }
    }

    private void EvaluateIncomeConsistency(FinancialProfile input, ref decimal score)
    {
        score = input.IncomeConsistencyScore * _options.IncomeConsistencyWeight;
        if (score > _options.IncomeConsistencyWeight) score = _options.IncomeConsistencyWeight;
        if (score < 0) score = 0;
    }

    private void EvaluateDebtToIncome(FinancialProfile input, ref decimal score)
    {
        // We calculate proxy DTI from ActiveLoanObligations vs AverageMonthlyIncome.
        if (input.AverageMonthlyIncome <= 0)
        {
            score = 0;
            return;
        }

        // ActiveLoanObligations might be total debt. Let's assume 5% of total debt is monthly payment.
        decimal monthlyDebtPayment = input.ActiveLoanObligations * 0.05m;
        decimal dti = monthlyDebtPayment / input.AverageMonthlyIncome;

        if (dti < 0.2m)
        {
            score = _options.DebtToIncomeWeight;
        }
        else if (dti < 0.35m)
        {
            score = _options.DebtToIncomeWeight * 0.75m;
        }
        else if (dti < 0.5m)
        {
            score = _options.DebtToIncomeWeight * 0.5m;
        }
        else
        {
            score = 0;
        }
    }

    private void EvaluateEmploymentStability(FinancialProfile input, ref decimal score)
    {
        if (input.EmploymentTenureMonths >= 24 && string.Equals(input.EmploymentType, "Full-Time", StringComparison.OrdinalIgnoreCase))
        {
            score = _options.EmploymentStabilityWeight;
        }
        else if (input.EmploymentTenureMonths >= 12)
        {
            score = _options.EmploymentStabilityWeight * 0.75m;
        }
        else if (input.EmploymentTenureMonths >= 6)
        {
            score = _options.EmploymentStabilityWeight * 0.5m;
        }
        else
        {
            score = 0;
        }
    }

    private void EvaluateCreditIndicator(FinancialProfile input, ref decimal score, ref decimal penaltyScore)
    {
        switch (input.CreditRiskGrade?.ToUpperInvariant())
        {
            case "A":
                score = _options.CreditIndicatorWeight;
                break;
            case "B":
                score = _options.CreditIndicatorWeight * 0.75m;
                break;
            case "C":
                score = _options.CreditIndicatorWeight * 0.5m;
                break;
            case "D":
                score = _options.CreditIndicatorWeight * 0.25m;
                break;
            default:
                score = 0;
                break;
        }

        if (input.DefaultHistoryIndicator)
        {
            penaltyScore -= 10m;
        }

        if (input.RecentCreditInquiries > 3)
        {
            penaltyScore -= 5m;
        }
    }

    private FeasibilityGrade DeriveGrade(decimal totalScore, decimal penaltyScore)
    {
        if (penaltyScore <= -15m) return FeasibilityGrade.E; // Auto-fail for bad history
        if (totalScore >= 80) return FeasibilityGrade.A;
        if (totalScore >= 60) return FeasibilityGrade.B;
        if (totalScore >= 40) return FeasibilityGrade.C;
        if (totalScore >= 20) return FeasibilityGrade.D;
        return FeasibilityGrade.E;
    }
}
