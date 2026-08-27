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
        var score = new FeasibilityScoreBreakdown(20, 20, 10, 10, 0, 60);
        Assert.Equal(60, score.TotalScore);
    }

    [Fact]
    public void FeasibilityScoreBreakdown_WithPositivePenalty_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FeasibilityScoreBreakdown(20, 20, 10, 10, 5, 60));
    }

    [Fact]
    public void FeasibilityScoreBreakdown_WithOutOfBoundsTotal_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FeasibilityScoreBreakdown(20, 20, 10, 10, 0, 105));
    }
}
