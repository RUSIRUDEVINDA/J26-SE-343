using System;
using System.Globalization;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class FinancialFeasibilityScoringEngineTests
{
    private static readonly DateTimeOffset EvaluationTime =
        new(2026, 8, 13, 10, 0, 0, TimeSpan.FromHours(5.5));

    [Theory]
    [InlineData("29.99", FeasibilityGrade.E, FeasibilityAction.Reject)]
    [InlineData("30.00", FeasibilityGrade.D, FeasibilityAction.Escalate)]
    [InlineData("49.99", FeasibilityGrade.D, FeasibilityAction.Escalate)]
    [InlineData("50.00", FeasibilityGrade.C, FeasibilityAction.ManualReview)]
    [InlineData("69.99", FeasibilityGrade.C, FeasibilityAction.ManualReview)]
    [InlineData("70.00", FeasibilityGrade.B, FeasibilityAction.Proceed)]
    [InlineData("84.99", FeasibilityGrade.B, FeasibilityAction.Proceed)]
    [InlineData("85.00", FeasibilityGrade.A, FeasibilityAction.FastTrack)]
    public void Component2V1_GradeBoundaries_MapToApprovedAction(
        string scoreText,
        FeasibilityGrade expectedGrade,
        FeasibilityAction expectedAction)
    {
        var contract = LeaseFeasibilityScoringContract.Component2V1;
        var score = decimal.Parse(scoreText, CultureInfo.InvariantCulture);

        var grade = contract.DeriveGrade(score);

        Assert.Equal(expectedGrade, grade);
        Assert.Equal(expectedAction, contract.DeriveAction(grade));
    }

    [Fact]
    public void EvaluateFeasibility_WorkedExample_Returns65CAndManualReview()
    {
        var engine = new FinancialFeasibilityScoringEngine();
        var input = CreateInput(
            averageMonthlyIncomeLkr: 100_000m,
            monthlyDebtObligationsLkr: 18_000m,
            requestedMonthlyLeasePaymentLkr: 10_000m,
            incomeConsistencyRatio: 1m,
            averageAccountBalanceLkr: 40_000m,
            overdraftCountLastSixMonths: 4,
            creditRiskGrade: CreditRiskGrade.B);

        var result = engine.EvaluateFeasibility(input, EvaluationTime);

        Assert.Equal(0.28m, result.ScoreBreakdown.DebtServiceRatio);
        Assert.Equal(4m, result.ScoreBreakdown.LiquidityBufferMonths);
        Assert.Equal(25m, result.ScoreBreakdown.DebtServiceRatioScore);
        Assert.Equal(25m, result.ScoreBreakdown.IncomeConsistencyScore);
        Assert.Equal(15m, result.ScoreBreakdown.LiquidityBufferScore);
        Assert.Equal(15m, result.ScoreBreakdown.CreditHistoryScore);
        Assert.Equal(-15m, result.ScoreBreakdown.PenaltyScore);
        Assert.Equal(65m, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.C, result.Grade);
        Assert.Equal(FeasibilityAction.ManualReview, result.Action);
    }

    [Theory]
    [InlineData(10_000, 35)] // (10,000 debt + 10,000 lease) / 100,000 = 20%
    [InlineData(10_001, 25)]
    [InlineData(25_000, 25)] // 35%
    [InlineData(25_001, 10)]
    [InlineData(40_000, 10)] // 50%
    [InlineData(40_001, 0)]
    public void EvaluateFeasibility_DebtServiceBoundaries_AreInclusiveAtUpperLimit(
        int monthlyDebtObligationsLkr,
        int expectedPoints)
    {
        var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(
            CreateInput(monthlyDebtObligationsLkr: monthlyDebtObligationsLkr),
            EvaluationTime);

        Assert.Equal(expectedPoints, result.ScoreBreakdown.DebtServiceRatioScore);
    }

    [Theory]
    [InlineData(9_999, 0)]
    [InlineData(10_000, 5)]
    [InlineData(29_999, 5)]
    [InlineData(30_000, 15)]
    [InlineData(59_999, 15)]
    [InlineData(60_000, 20)]
    public void EvaluateFeasibility_LiquidityBoundaries_UseRequestedLeasePayment(
        int averageAccountBalanceLkr,
        int expectedPoints)
    {
        var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(
            CreateInput(averageAccountBalanceLkr: averageAccountBalanceLkr),
            EvaluationTime);

        Assert.Equal(expectedPoints, result.ScoreBreakdown.LiquidityBufferScore);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, -15)]
    public void EvaluateFeasibility_OverdraftPenalty_AppliesOnlyAboveApprovedThreshold(
        int overdraftCountLastSixMonths,
        int expectedPenalty)
    {
        var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(
            CreateInput(overdraftCountLastSixMonths: overdraftCountLastSixMonths),
            EvaluationTime);

        Assert.Equal(expectedPenalty, result.ScoreBreakdown.PenaltyScore);
    }

    [Fact]
    public void EvaluateFeasibility_PreservesDistinctIdsAndTimeProviderTimestamp()
    {
        var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(
            CreateInput(applicationId: "LEASE-42", applicantId: "PERSON-7"),
            EvaluationTime);

        Assert.Equal("LEASE-42", result.ApplicationId);
        Assert.Equal("PERSON-7", result.ApplicantId);
        Assert.Equal(EvaluationTime.ToUniversalTime(), result.GeneratedAt);
        Assert.Equal(LeaseFeasibilityScoringContract.Component2V1.Version, result.ContractVersion);
    }

    [Fact]
    public void EvaluateFeasibility_UsesExplicitMonthlyDebtWithoutFivePercentConversion()
    {
        var result = new FinancialFeasibilityScoringEngine().EvaluateFeasibility(
            CreateInput(
                averageMonthlyIncomeLkr: 100_000m,
                requestedMonthlyLeasePaymentLkr: 10_000m,
                monthlyDebtObligationsLkr: 40_000m),
            EvaluationTime);

        Assert.Equal(0.50m, result.ScoreBreakdown.DebtServiceRatio);
        Assert.Equal(10m, result.ScoreBreakdown.DebtServiceRatioScore);
    }

    private static FinancialFeasibilityScoringInput CreateInput(
        string applicationId = "LEASE-001",
        string applicantId = "PERSON-001",
        decimal averageMonthlyIncomeLkr = 100_000m,
        decimal incomeConsistencyRatio = 1m,
        decimal requestedMonthlyLeasePaymentLkr = 10_000m,
        decimal monthlyDebtObligationsLkr = 0m,
        decimal averageAccountBalanceLkr = 60_000m,
        int overdraftCountLastSixMonths = 0,
        CreditRiskGrade creditRiskGrade = CreditRiskGrade.A,
        bool hasDefaultHistory = false)
    {
        return new FinancialFeasibilityScoringInput(
            applicationId,
            applicantId,
            averageMonthlyIncomeLkr,
            incomeConsistencyRatio,
            requestedMonthlyLeasePaymentLkr,
            monthlyDebtObligationsLkr,
            averageAccountBalanceLkr,
            overdraftCountLastSixMonths,
            creditRiskGrade,
            hasDefaultHistory);
    }
}
