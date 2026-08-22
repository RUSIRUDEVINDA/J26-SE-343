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
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Azure:DocumentIntelligence:Endpoint"]).Returns("https://dummy.cognitiveservices.azure.com/");
        configMock.Setup(c => c["Azure:DocumentIntelligence:Key"]).Returns("dummy-key");

        var service = new DocumentOcrService(configMock.Object);

        // Act
        var result = await service.ExtractTextAsync("mock-statement.txt");

        // Assert
        _output.WriteLine("--- RAW EXTRACTED TEXT FROM OCR ---");
        _output.WriteLine(result);
        _output.WriteLine("-----------------------------------");
        Assert.Contains("MOCK OCR RESULT", result);
    }
}
