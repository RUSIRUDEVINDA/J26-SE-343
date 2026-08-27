using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StateLandGovernance.LeaseFeasibility.Application.Commands;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Application.Commands;

public class AssessFinancialFeasibilityCommandHandlerTests
{
    private class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public TestTimeProvider(DateTimeOffset time) => _time = time;
        public override DateTimeOffset GetUtcNow() => _time;
    }

    [Fact]
    public async Task HandleAsync_ValidUris_ExtractsDataAndPersistsAssessment()
    {
        // Arrange
        var extractionMock = new Mock<IDocumentExtractionService>();
        
        extractionMock.Setup(e => e.ExtractBankStatementDataAsync("mock-statement.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankStatementDataDto(5000, 15000, 0, 0.2m));
            
        extractionMock.Setup(e => e.ExtractSalarySlipDataAsync("mock-salary1.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalarySlipDataDto(6500, 24, "Full-Time", "Acme Corp"));
            
        extractionMock.Setup(e => e.ExtractCribReportDataAsync("mock-crib.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CribReportDataDto("A", 12000, false, 1));

        var scoringEngineMock = new Mock<IFinancialFeasibilityScoringEngine>();
        
        var breakdown = new StateLandGovernance.LeaseFeasibility.Domain.ValueObjects.FeasibilityScoreBreakdown(
            debtToIncomeScore: 20m,
            incomeConsistencyScore: 20m,
            liquidityBufferScore: 15m,
            creditHistoryScore: 15m,
            penaltyScore: 0m,
            totalScore: 70m
        );
        
        scoringEngineMock.Setup(e => e.EvaluateFeasibility(It.IsAny<FinancialProfileDto>(), It.IsAny<DateTime>()))
            .Returns(new FinancialFeasibilityAssessment("APP-123", FeasibilityGrade.B, breakdown, null));

        var repoMock = new Mock<IFinancialFeasibilityRepository>();
        var loggerMock = new Mock<ILogger<AssessFinancialFeasibilityCommandHandler>>();
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);

        var handler = new AssessFinancialFeasibilityCommandHandler(
            extractionMock.Object,
            scoringEngineMock.Object,
            repoMock.Object,
            timeProvider,
            loggerMock.Object
        );

        var command = new AssessFinancialFeasibilityCommand(
            ApplicationId: "APP-123",
            ApplicantId: "APP-SYNTH-001",
            BankStatementUri: "mock-statement.txt",
            SalarySlipUri: "mock-salary1.txt",
            CribReportUri: "mock-crib.txt"
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("APP-123", result.ApplicationId);
        Assert.Equal("APP-SYNTH-001", result.ApplicantId);
        Assert.Equal(70, result.TotalScore);
        Assert.Equal("B", result.EligibilityGrade);
        
        // Verify dependencies were called
        extractionMock.Verify(e => e.ExtractBankStatementDataAsync("mock-statement.txt", It.IsAny<CancellationToken>()), Times.Once);
        extractionMock.Verify(e => e.ExtractSalarySlipDataAsync("mock-salary1.txt", It.IsAny<CancellationToken>()), Times.Once);
        extractionMock.Verify(e => e.ExtractCribReportDataAsync("mock-crib.txt", It.IsAny<CancellationToken>()), Times.Once);
        
        scoringEngineMock.Verify(e => e.EvaluateFeasibility(It.Is<FinancialProfileDto>(p => p.ApplicantId == "APP-SYNTH-001"), It.IsAny<DateTime>()), Times.Once);
        repoMock.Verify(r => r.AddAsync(It.Is<FinancialFeasibilityAssessment>(a => a.ApplicationId == "APP-123"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
