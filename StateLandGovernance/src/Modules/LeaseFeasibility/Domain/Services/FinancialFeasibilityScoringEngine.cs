using System;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Pure deterministic implementation of the Component 2 scoring contract.
/// </summary>
public sealed class FinancialFeasibilityScoringEngine : IFinancialFeasibilityScoringEngine
{
    private readonly LeaseFeasibilityScoringContract _contract;

    public FinancialFeasibilityScoringEngine()
        : this(LeaseFeasibilityScoringContract.Component2V1)
    {
    }

    public FinancialFeasibilityScoringEngine(LeaseFeasibilityScoringContract contract)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        _contract.Validate();
    }

    public FinancialFeasibilityAssessment EvaluateFeasibility(
        FinancialFeasibilityScoringInput input,
        DateTimeOffset evaluationTimestamp)
    {
        ArgumentNullException.ThrowIfNull(input);

        var debtServiceRatio =
            (input.MonthlyDebtObligationsLkr + input.RequestedMonthlyLeasePaymentLkr) /
            input.AverageMonthlyIncomeLkr;
        var liquidityBufferMonths = input.AverageAccountBalanceLkr / input.RequestedMonthlyLeasePaymentLkr;

        var debtServiceRatioScore = ScoreDebtServiceRatio(debtServiceRatio);
        var incomeConsistencyScore = Math.Round(
            input.IncomeConsistencyRatio * _contract.IncomeConsistencyWeight,
            2,
            MidpointRounding.AwayFromZero);
        var liquidityBufferScore = ScoreLiquidityBuffer(liquidityBufferMonths);
        var creditHistoryScore = ScoreCreditHistory(input.CreditRiskGrade);
        var penaltyScore = ScorePenalties(input);

        var rawScore = debtServiceRatioScore + incomeConsistencyScore + liquidityBufferScore +
                       creditHistoryScore + penaltyScore;
        var totalScore = Math.Clamp(rawScore, 0m, 100m);
        var grade = _contract.DeriveGrade(totalScore);
        var action = _contract.DeriveAction(grade);

        var breakdown = new FeasibilityScoreBreakdown(
            debtServiceRatio,
            liquidityBufferMonths,
            debtServiceRatioScore,
            incomeConsistencyScore,
            liquidityBufferScore,
            creditHistoryScore,
            penaltyScore,
            totalScore);

        return new FinancialFeasibilityAssessment(
            applicationId: input.ApplicationId,
            applicantId: input.ApplicantId,
            contractVersion: _contract.Version,
            grade: grade,
            action: action,
            scoreBreakdown: breakdown,
            generatedAt: evaluationTimestamp,
            predictiveProbability: null);
    }

    private decimal ScoreDebtServiceRatio(decimal ratio)
    {
        if (ratio <= _contract.StrongDebtServiceRatioMaximum)
        {
            return _contract.StrongDebtServiceRatioPoints;
        }

        if (ratio <= _contract.ManageableDebtServiceRatioMaximum)
        {
            return _contract.ManageableDebtServiceRatioPoints;
        }

        if (ratio <= _contract.MarginalDebtServiceRatioMaximum)
        {
            return _contract.MarginalDebtServiceRatioPoints;
        }

        return 0m;
    }

    private decimal ScoreLiquidityBuffer(decimal months)
    {
        if (months >= _contract.FullLiquidityBufferMonths)
        {
            return _contract.FullLiquidityBufferPoints;
        }

        if (months >= _contract.AdequateLiquidityBufferMonths)
        {
            return _contract.AdequateLiquidityBufferPoints;
        }

        if (months >= _contract.MinimumLiquidityBufferMonths)
        {
            return _contract.MinimumLiquidityBufferPoints;
        }

        return 0m;
    }

    private decimal ScoreCreditHistory(CreditRiskGrade grade) => grade switch
    {
        CreditRiskGrade.A => _contract.CreditGradeAPoints,
        CreditRiskGrade.B => _contract.CreditGradeBPoints,
        CreditRiskGrade.C => _contract.CreditGradeCPoints,
        CreditRiskGrade.D or CreditRiskGrade.E => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unknown credit risk grade.")
    };

    private decimal ScorePenalties(FinancialFeasibilityScoringInput input)
    {
        var penalty = input.HasDefaultHistory ? _contract.DefaultHistoryPenalty : 0m;

        if (input.OverdraftCountInEvidenceWindow > _contract.FrequentOverdraftThreshold)
        {
            penalty += _contract.FrequentOverdraftPenalty;
        }

        return penalty;
    }
}
