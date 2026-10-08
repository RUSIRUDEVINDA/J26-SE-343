using System;
using Xunit;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class ValueObjectsTests
{
    [Fact]
    public void IncomeProfile_WithValidInputs_InitializesCorrectly()
    {
        var profile = new IncomeProfile("APP-001", 5000m, 0.8m, 24, EmploymentType.Salaried, "Corp Inc");
        Assert.Equal("APP-001", profile.ApplicantId);
        Assert.Equal(5000m, profile.AverageMonthlyIncome);
    }

    [Fact]
    public void IncomeProfile_WithNegativeIncome_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new IncomeProfile("APP-001", -10m, 0.8m, 24, EmploymentType.Salaried));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void IncomeProfile_WithInvalidConsistencyScore_ThrowsArgumentException(decimal score)
    {
        Assert.Throws<ArgumentException>(() => new IncomeProfile("APP-001", 5000m, score, 24, EmploymentType.Salaried));
    }

    [Fact]
    public void FinancialDocumentEvidence_WithValidInputs_InitializesCorrectly()
    {
        var date = DateTime.UtcNow;
        var doc = new FinancialDocumentEvidence("DOC-001", DocumentType.BankStatement, "APP-001", "Bank A", date);
        Assert.Equal("DOC-001", doc.DocumentId);
    }

    [Fact]
    public void FinancialDocumentEvidence_WithEmptyInstitution_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FinancialDocumentEvidence("DOC-001", DocumentType.BankStatement, "APP-001", "", DateTime.UtcNow));
    }

    [Fact]
    public void ApprovalProbability_WithValidProbability_InitializesCorrectly()
    {
        var prob = new ApprovalProbability("v1.0", 0.75m, RiskLevel.Moderate);
        Assert.Equal(0.75m, prob.Probability);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void ApprovalProbability_WithOutOfBoundsProbability_ThrowsArgumentOutOfRangeException(decimal probability)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApprovalProbability("v1.0", probability, RiskLevel.Moderate));
    }

    [Fact]
    public void FeasibilityScoreBreakdown_WithValidScores_InitializesCorrectly()
    {
        var score = new FeasibilityScoreBreakdown(0.30m, 4m, 25m, 20m, 15m, 15m, 0m, 75m);
        Assert.Equal(75m, score.TotalScore);
    }

    [Fact]
    public void FeasibilityScoreBreakdown_WithPositivePenalty_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FeasibilityScoreBreakdown(0.30m, 4m, 25m, 20m, 15m, 15m, 5m, 75m));
    }

    [Fact]
    public void FeasibilityScoreBreakdown_WithOutOfBoundsTotal_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FeasibilityScoreBreakdown(0.30m, 4m, 25m, 20m, 15m, 15m, 0m, 105m));
    }

    [Fact]
    public void FinancialFeasibilityScoringInput_WithInvalidRatio_ThrowsDomainException()
    {
        Assert.Throws<StateLandGovernance.LeaseFeasibility.Domain.Exceptions.InvalidFinancialProfileException>(() =>
            new FinancialFeasibilityScoringInput(
                "APP-001",
                "PERSON-001",
                100_000m,
                1.01m,
                10_000m,
                5_000m,
                30_000m,
                0,
                CreditRiskGrade.A,
                false));
    }
}
