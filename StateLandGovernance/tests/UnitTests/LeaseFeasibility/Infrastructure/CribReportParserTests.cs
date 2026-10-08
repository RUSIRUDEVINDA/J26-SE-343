using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public class CribReportParserTests
{
    [Theory]
    [InlineData("A1", "A")]
    [InlineData("B3", "B")]
    [InlineData("C2", "C")]
    [InlineData("D1", "D")]
    [InlineData("E3", "E")]
    public void PreservesSubgradeAndSeparatesBureauProbability(string raw, string band)
    {
        var result = CribReportParser.Parse($"Risk Grade: {raw}\nCredit Score: 550\nProbability of Default: 20.5%\nTotal Current Balance: LKR 150,000.00");
        Assert.Equal(raw, result.CreditRiskGrade);
        Assert.Equal(band, result.NormalizedCreditRiskGrade);
        Assert.Equal(550, result.CreditScore);
        Assert.Equal(20.5m, result.BureauProbabilityOfDefaultPercent);
        Assert.Equal(150000m, result.OutstandingBalanceLkr);
        Assert.Null(result.ReportedMonthlyPaymentsLkr);
        Assert.Null(result.ActiveLoanObligations);
        Assert.Null(result.DefaultHistoryIndicator);
        Assert.True(result.RequiresManualReview);
    }

    [Fact]
    public void InsufficientInformationRemainsUnscored()
    {
        var result = CribReportParser.Parse("Risk Grade: XX\nTotal Monthly Payments: 0\nCurrent Balance: 90000");
        Assert.Null(result.NormalizedCreditRiskGrade);
        Assert.Null(result.ReportedMonthlyPaymentsLkr);
        Assert.True(result.RequiresManualReview);
    }

    [Fact]
    public void ReviewedMonthlyDebtDoesNotComeFromBalance()
    {
        var result = CribReportParser.Parse("Risk Grade: B1\nTotal Current Balance: 100000\nReviewed Monthly Debt Obligations LKR: 2500\nReviewed Default History Indicator: false\nRecent Credit Inquiries: 2\nSelf Inquiries Last 6 Months: 1");
        Assert.Equal(2500m, result.ReportedMonthlyPaymentsLkr);
        Assert.Equal(100000m, result.OutstandingBalanceLkr);
        Assert.False(result.DefaultHistoryIndicator);
        Assert.Equal(2, result.RecentCreditInquiries);
        Assert.Equal(1, result.SelfInquiriesLastSixMonths);
        Assert.False(result.RequiresManualReview);
    }

    [Theory]
    [InlineData("Risk Grade: B4")]
    [InlineData("Risk Grade: B1\nCredit Score: 100")]
    [InlineData("Risk Grade: B1\nProbability of Default: 101%")]
    public void InvalidEvidenceIsRejected(string text) =>
        Assert.Throws<ValidationException>(() => CribReportParser.Parse(text));

    [Fact]
    public void RepeatedBalancesAreNotSummedOrChosenArbitrarily()
    {
        var result = CribReportParser.Parse("Risk Grade: B1\nTotal Current Balance: 100\nTotal Current Balance: 200\nActive Disputes: 1");
        Assert.Null(result.OutstandingBalanceLkr);
        Assert.True(result.RequiresManualReview);
    }
}
