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
}
