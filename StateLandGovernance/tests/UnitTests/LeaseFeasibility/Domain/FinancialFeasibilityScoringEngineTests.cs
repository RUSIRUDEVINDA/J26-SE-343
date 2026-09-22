using System;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class FinancialFeasibilityScoringEngineTests
{
    private readonly DateTime _baseTime = new(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt19_ReturnsEGrade()
    {
        // Custom engine options: total weight = 19
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 19,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(19, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.E, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt20_ReturnsDGrade()
    {
        // Custom engine options: total weight = 20
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 20,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(20, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.D, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt39_ReturnsDGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 39,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(39, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.D, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt40_ReturnsCGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 40,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(40, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.C, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt59_ReturnsCGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 59,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(59, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.C, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt60_ReturnsBGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 60,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(60, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.B, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt79_ReturnsBGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 79,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(79, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.B, result.Grade);
    }

    [Fact]
    public void EvaluateFeasibility_ScoreBoundaryAt80_ReturnsAGrade()
    {
        var options = new LeaseFeasibilityScoringOptions(
            IncomeToLeaseCostRatioWeight: 80,
            IncomeConsistencyWeight: 0,
            DebtToIncomeWeight: 0,
            EmploymentStabilityWeight: 0,
            CreditIndicatorWeight: 0);

        var engine = new FinancialFeasibilityScoringEngine(options);

        var profile = CreateMaxScoringProfile();

        var result = engine.EvaluateFeasibility(profile, _baseTime);

        Assert.Equal(80, result.ScoreBreakdown.TotalScore);
        Assert.Equal(FeasibilityGrade.A, result.Grade);
    }

    private FinancialProfile CreateMaxScoringProfile()
    {
        return new FinancialProfile(
            ApplicantId: "APP-001",
            AverageMonthlyIncome: 10000m,
            IncomeConsistencyScore: 1.0m,
            EmploymentTenureMonths: 36,
            EmploymentType: "Full-Time",
            EmployerOrBusinessName: "Corp Inc",
            AverageAccountBalance: 50000m,
            OverdraftFrequency: 0,
            SavingsToIncomeRatio: 0.5m,
            CreditRiskGrade: "A",
            ActiveLoanObligations: 0m,
            DefaultHistoryIndicator: false,
            RecentCreditInquiries: 0
        );
    }
}
