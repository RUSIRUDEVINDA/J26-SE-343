using System;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Utilities;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Application.Utilities;

public class PiiMaskerTests
{
    [Fact]
    public void GetMaskedLogPayload_MasksNamesAndIds_PrivacyProtected()
    {
        // Arrange
        var rawApplicantId = "APP-SECRET-999";
        var rawEmployerName = "Acme Confidential Corp";

        var profile = new FinancialProfileDto(
            ApplicantId: rawApplicantId,
            AverageMonthlyIncome: 8500m,
            IncomeConsistencyScore: 0.9m,
            EmploymentTenureMonths: 36,
            EmploymentType: "Full-Time",
            EmployerOrBusinessName: rawEmployerName,
            AverageAccountBalance: 20000m,
            OverdraftFrequency: 0,
            SavingsToIncomeRatio: 0.3m,
            CreditRiskGrade: "A",
            ActiveLoanObligations: 5000m,
            DefaultHistoryIndicator: false,
            RecentCreditInquiries: 0
        );

        // Act
        var logPayload = PiiMasker.GetMaskedLogPayload(profile);

        // Assert - verify raw PII does not exist in the output
        Assert.DoesNotContain(rawApplicantId, logPayload);
        Assert.DoesNotContain(rawEmployerName, logPayload);
        
        // Verify it contains the masked equivalents
        Assert.Contains("APP-***", logPayload);
        Assert.Contains("A********************p", logPayload);
        
        // Verify metadata-only conventions
        Assert.Contains("[Provided]", logPayload);
        Assert.DoesNotContain("8500", logPayload); // Raw income should not be exposed
    }

    [Theory]
    [InlineData("123456789", "*****6789")]
    [InlineData("123", "***")]
    [InlineData("", "")]
    public void MaskAccountNumber_WorksCorrectly(string input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskAccountNumber(input));
    }
}
