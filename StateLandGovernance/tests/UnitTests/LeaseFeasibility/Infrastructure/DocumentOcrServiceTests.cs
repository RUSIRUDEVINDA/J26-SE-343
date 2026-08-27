using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public class DocumentOcrServiceTests
{
    private readonly ITestOutputHelper _output;

    public DocumentOcrServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExtractTextAsync_WithMockDocument_ReturnsExtractedText()
    {
        // Arrange
        var envEndpoint = Environment.GetEnvironmentVariable("TEST_OCR_ENDPOINT") ?? "https://dummy.cognitiveservices.azure.com/";
        var envKey = Environment.GetEnvironmentVariable("TEST_OCR_KEY") ?? "dummy-key";
        var envFile = Environment.GetEnvironmentVariable("TEST_OCR_FILE") ?? "mock-statement.txt";

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns(envEndpoint);
        configMock.Setup(c => c["Azure:DocumentIntelligence:Key"]).Returns(envKey);

        var service = new DocumentOcrService(configMock.Object);

        // Act
        var result = await service.ExtractTextAsync(envFile);

        // Assert
        _output.WriteLine("--- RAW EXTRACTED TEXT FROM OCR ---");
        _output.WriteLine(result);
        _output.WriteLine("-----------------------------------");
        
        if (envFile == "mock-statement.txt")
        {
            Assert.Contains("MOCK OCR RESULT", result);
        }
        else
        {
            Assert.NotEmpty(result);
        }
    }

    [Fact]
    public async Task ExtractBankStatementDataAsync_WithMockDocument_ReturnsStructuredData()
    {
        // Arrange
        var envEndpoint = Environment.GetEnvironmentVariable("TEST_OCR_ENDPOINT") ?? "https://dummy.cognitiveservices.azure.com/";
        var envKey = Environment.GetEnvironmentVariable("TEST_OCR_KEY") ?? "dummy-key";
        var envFile = Environment.GetEnvironmentVariable("TEST_OCR_FILE") ?? "mock-statement.txt";

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns(envEndpoint);
        configMock.Setup(c => c["Azure:DocumentIntelligence:Key"]).Returns(envKey);

        var service = new DocumentOcrService(configMock.Object);

        // Act
        var result = await service.ExtractBankStatementDataAsync(envFile);

        // Assert
        if (envFile == "mock-statement.txt")
        {
            Assert.Equal(5000m, result.AverageMonthlyIncome);
            Assert.Equal(15000m, result.AverageAccountBalance);
            Assert.Equal(0, result.OverdraftFrequency);
            Assert.Equal(0.2m, result.SavingsToIncomeRatio);
        }
        else
        {
            Assert.NotNull(result);
        }
    }

    [Theory]
    [InlineData("mock-salary1.txt", 6500, 24, "Full-Time", "Acme Corp")]
    [InlineData("mock-salary2.txt", 8200, 12, "Contract", "Globex Inc")]
    [InlineData("mock-salary3.txt", 4000, 6, "Part-Time", "Initech")]
    public async Task ExtractSalarySlipDataAsync_WithMockDocuments_ReturnsStructuredData(
        string fileName, decimal expectedIncome, int expectedTenure, string expectedType, string expectedEmployer)
    {
        // Arrange
        var envEndpoint = Environment.GetEnvironmentVariable("TEST_OCR_ENDPOINT") ?? "https://dummy.cognitiveservices.azure.com/";
        var envKey = Environment.GetEnvironmentVariable("TEST_OCR_KEY") ?? "dummy-key";

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns(envEndpoint);
        configMock.Setup(c => c["Azure:DocumentIntelligence:Key"]).Returns(envKey);

        var service = new DocumentOcrService(configMock.Object);

        // Act
        var result = await service.ExtractSalarySlipDataAsync(fileName);

        // Assert
        Assert.Equal(expectedIncome, result.AverageMonthlyIncome);
        Assert.Equal(expectedTenure, result.EmploymentTenureMonths);
        Assert.Equal(expectedType, result.EmploymentType);
        Assert.Equal(expectedEmployer, result.EmployerOrBusinessName);
    }

    [Fact]
    public async Task AssembleCompleteFinancialProfile_FromMultipleDocuments_ReturnsUnifiedProfile()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
        var service = new DocumentOcrService(configMock.Object);

        var bankData = await service.ExtractBankStatementDataAsync("mock-statement.txt");
        var salaryData = await service.ExtractSalarySlipDataAsync("mock-salary1.txt");
        var cribData = await service.ExtractCribReportDataAsync("mock-crib.txt");

        var profile = new StateLandGovernance.LeaseFeasibility.Application.DTOs.FinancialProfileDto(
            ApplicantId: "APP-SYNTH-001",
            AverageMonthlyIncome: salaryData.AverageMonthlyIncome,
            IncomeConsistencyScore: 0.85m, // Synthesized
            EmploymentTenureMonths: salaryData.EmploymentTenureMonths,
            EmploymentType: salaryData.EmploymentType,
            EmployerOrBusinessName: salaryData.EmployerOrBusinessName,
            AverageAccountBalance: bankData.AverageAccountBalance,
            OverdraftFrequency: bankData.OverdraftFrequency,
            SavingsToIncomeRatio: bankData.SavingsToIncomeRatio,
            CreditRiskGrade: cribData.CreditRiskGrade,
            ActiveLoanObligations: cribData.ActiveLoanObligations,
            DefaultHistoryIndicator: cribData.DefaultHistoryIndicator,
            RecentCreditInquiries: cribData.RecentCreditInquiries
        );

        var json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        _output.WriteLine("--- ASSEMBLED FINANCIAL PROFILE ---");
        _output.WriteLine(json);

        Assert.NotNull(profile);
        Assert.Equal("A", profile.CreditRiskGrade);
        Assert.Equal(6500m, profile.AverageMonthlyIncome);
    }

    [Fact]
    public async Task ExtractSalarySlipDataAsync_WithMalformedDocument_ThrowsValidationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
        var service = new DocumentOcrService(configMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<StateLandGovernance.LeaseFeasibility.Application.Interfaces.ValidationException>(
            () => service.ExtractSalarySlipDataAsync("mock-salary-malformed.txt")
        );

        Assert.NotEmpty(ex.Errors);
        Assert.Contains(ex.Errors, e => e.Contains("SALARY SLIP' indicator not found"));
        Assert.Contains(ex.Errors, e => e.Contains("Missing required field: Average Monthly Income"));
    }

    [Fact]
    public async Task ExtractBankStatementDataAsync_WithMissingFields_ThrowsValidationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
        var service = new DocumentOcrService(configMock.Object);
        
        string tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, "[BANK STATEMENT]\nAverage Monthly Income: 5000.\nOverdraft Frequency: 1"); // Missing Account Balance and Savings Ratio

        try
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<StateLandGovernance.LeaseFeasibility.Application.Interfaces.ValidationException>(
                () => service.ExtractBankStatementDataAsync(tempFile)
            );

            Assert.NotEmpty(ex.Errors);
            Assert.Contains(ex.Errors, e => e.Contains("Missing required field: Average Account Balance"));
            Assert.Contains(ex.Errors, e => e.Contains("Missing required field: Savings To Income Ratio"));
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExtractCribReportDataAsync_WithMissingFields_ThrowsValidationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
        var service = new DocumentOcrService(configMock.Object);
        
        string tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, "[CRIB REPORT]\nActive Loan Obligations: 5000.\nDefault History Indicator: True"); // Missing Grade and Inquiries

        try
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<StateLandGovernance.LeaseFeasibility.Application.Interfaces.ValidationException>(
                () => service.ExtractCribReportDataAsync(tempFile)
            );

            Assert.NotEmpty(ex.Errors);
            Assert.Contains(ex.Errors, e => e.Contains("Missing required field: Credit Risk Grade"));
            Assert.Contains(ex.Errors, e => e.Contains("Missing required field: Recent Credit Inquiries"));
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }
}
