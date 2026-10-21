using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public sealed class FinancialTableFieldMapperTests
{
    [Fact]
    public void SixMonthBankTablesMapSalaryDailyBalanceAndOverdrafts()
    {
        var tables = BankTables();
        var result = FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields("bank", "Statement"), tables);
        Assert.Equal("120000", result.Fields["income"]);
        Assert.Equal("0", result.Fields["overdrafts"]);
        Assert.True(decimal.Parse(result.Fields["balance"], System.Globalization.CultureInfo.InvariantCulture) > 10000);
        Assert.False(result.Fields.ContainsKey("consistency"));
        Assert.False(result.Fields.ContainsKey("lease"));
        Assert.False(result.Fields.ContainsKey("debt"));
        Assert.Contains("salary", result.FieldSources["income"]);
    }

    [Fact]
    public void MissingMonthOrUnclearBalancePreventsDerivedValues()
    {
        var tables = BankTables();
        var missing = FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields("bank", "Statement"), tables.Take(5).ToArray());
        Assert.Empty(missing.Fields);
        tables[0][2][4] = "UNCLEAR";
        var unclear = FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields("bank", "Statement"), tables);
        Assert.Empty(unclear.Fields);
    }

    [Fact]
    public void RepeatedStatementMonthDoesNotDoubleCountIncome()
    {
        var tables = BankTables();
        var result = FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields("bank", "Statement"), tables.Concat(new[] { tables[0] }).ToArray());
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void CribCurrentGradeAndMonthlyBorrowerInstallmentsAreMapped()
    {
        var result = MapCrib("LKR");
        Assert.Equal("E3", result.Fields["grade"]);
        Assert.Equal("20000", result.Fields["debt"]);
        Assert.False(result.Fields.ContainsKey("defaultHistory"));
        Assert.Contains("installments", result.FieldSources["debt"]);
    }

    [Fact]
    public void MisreadCurrencyDoesNotProduceAnIncompleteDebtTotal()
    {
        var result = MapCrib("FOORT");
        Assert.Equal("E3", result.Fields["grade"]);
        Assert.False(result.Fields.ContainsKey("debt"));
        Assert.Contains(result.ReviewReasons, reason => reason.Contains("currency"));
    }

    private static UploadedFinancialDocumentResult MapCrib(string secondCurrency)
    {
        string[][][] tables = [
            [ ["Score", "Risk Grade", "Trend", "Probability of Default"], ["300", "E3", "Declining", "20%"] ],
            [ ["Credit Facility Index", "1", "2", "3"], ["Member", "L1", "L2", "L3"],
                ["Credit Facility Status", "ACTV", "ACTV", "ACTV"], ["Ownership Indicator", "OWN", "JNT", "GRT"],
                ["Currency", "LKR", secondCurrency, "LKR"], ["Repayment Type", "MNLY", "MNLY", "MNLY"],
                ["Installment Amount", "12,000.00", "8,000.00", "99,000.00"], ["Current Balance", "500,000", "600,000", "700,000"] ] ];
        return FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields("crib", "Report"), tables);
    }

    private static string[][][] BankTables() => Enumerable.Range(4, 6).Select(month =>
    {
        var start = new DateTime(2026, month, 1);
        string Date(int day) => new DateTime(2026, month, day).ToString("ddMMMyyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
        return new[] {
            new[] { "DATE", "PARTICULARS", "DEBITS", "CREDITS", "BALANCE" },
            new[] { Date(1), "B/F", "", "", "10,000.00Cr" },
            new[] { Date(15), "NSC SAL TEST EMPLOYER", "", "120,000.00", "130,000.00Cr" },
            new[] { Date(20), "OWN ACCOUNT TRANSFER", "", "50,000.00", "180,000.00Cr" },
            new[] { Date(DateTime.DaysInMonth(2026, month)), "C/F", "", "", "10,000.00Cr" }
        };
    }).ToArray();
}
