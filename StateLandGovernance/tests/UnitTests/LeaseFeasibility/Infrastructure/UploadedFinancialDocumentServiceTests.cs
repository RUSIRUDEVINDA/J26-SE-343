using Microsoft.Extensions.Configuration;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public sealed class UploadedFinancialDocumentServiceTests
{
    [Fact]
    public void OrdinaryTransactionsDoNotBecomeInventedIncome()
    {
        var result = UploadedFinancialDocumentService.MapFields("bank", "Date Credit Debit Balance\n01/01 185000 25000 480000");
        Assert.Empty(result.Fields);
        Assert.Contains(result.ReviewReasons, reason => reason.Contains("not automatically income"));
        Assert.Contains("185000", result.Text);
    }

    [Fact]
    public void ExplicitSummaryFieldsAreMappedFromTheDocument()
    {
        var result = UploadedFinancialDocumentService.MapFields("bank",
            "Average Monthly Income (LKR): 94,321.50\nAverage Account Balance: LKR 200,100.25\nIncome Consistency Ratio: 0.73");
        Assert.Equal("94321.50", result.Fields["income"]);
        Assert.Equal("200100.25", result.Fields["balance"]);
        Assert.Equal("0.73", result.Fields["consistency"]);
        Assert.False(result.Fields.ContainsKey("debt"));
    }

    [Fact]
    public void AmbiguousSummaryFieldsStayUnknown()
    {
        var result = UploadedFinancialDocumentService.MapFields("bank", "Average Monthly Income: 100\nAverage Monthly Income: 200");
        Assert.False(result.Fields.ContainsKey("income"));
    }

    [Fact]
    public void CribPrincipalIsNeverUsedAsMonthlyRepayment()
    {
        var result = UploadedFinancialDocumentService.MapFields("crib", "Risk Grade: C2\nTotal Current Balance: 950,000");
        Assert.Equal("C2", result.Fields["grade"]);
        Assert.False(result.Fields.ContainsKey("debt"));
        Assert.False(result.Fields.ContainsKey("defaultHistory"));
    }

    [Fact]
    public async Task MissingAzureConfigurationFailsRatherThanReturningMockData()
    {
        var service = new UploadedFinancialDocumentService(new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExtractAsync(
            new MemoryStream("%PDF-fixture"u8.ToArray()), "bank", "mock-bank.pdf", CancellationToken.None));
    }
}
