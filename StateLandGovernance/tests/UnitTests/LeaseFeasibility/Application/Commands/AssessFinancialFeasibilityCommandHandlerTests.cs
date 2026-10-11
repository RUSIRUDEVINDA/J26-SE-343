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
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Application.Commands;

public class AssessFinancialFeasibilityCommandHandlerTests
{
    [Theory]
    [InlineData("XX", false, 0)]
    [InlineData("B1", null, 0)]
    [InlineData("B1", false, 1)]
    public async Task UnreviewedCribEvidenceDoesNotScoreOrPersist(string grade, bool? history, int disputes)
    {
        var extraction = new Mock<IDocumentExtractionService>();
        extraction.Setup(e => e.ExtractBankStatementDataAsync("bank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankStatementDataDto(100000m, 40000m, 0, 0.2m));
        extraction.Setup(e => e.ExtractSalarySlipDataAsync("salary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalarySlipDataDto(100000m, 24, "Full-Time", "Synthetic employer"));
        extraction.Setup(e => e.ExtractCribReportDataAsync("crib", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CribReportDataDto(grade, null, history, null) { ActiveDisputes = disputes });
        var scoring = new Mock<IFinancialFeasibilityScoringEngine>(MockBehavior.Strict);
        var repository = new Mock<IFinancialFeasibilityRepository>(MockBehavior.Strict);
        var handler = new AssessFinancialFeasibilityCommandHandler(extraction.Object,
            scoring.Object, repository.Object, TimeProvider.System,
            Mock.Of<ILogger<AssessFinancialFeasibilityCommandHandler>>());
        await Assert.ThrowsAsync<StateLandGovernance.LeaseFeasibility.Application.Interfaces.ValidationException>(() =>
            handler.HandleAsync(new AssessFinancialFeasibilityCommand("case", "applicant", 10000m, 2000m, 0.8m, "bank", "salary", "crib")));
        scoring.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifiedTotalIncomeFeedsCapacityAndResponse()
    {
        var extraction = new Mock<IDocumentExtractionService>();
        extraction.Setup(e => e.ExtractBankStatementDataAsync("bank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankStatementDataDto(0m, 200000m, 0, 0m));
        extraction.Setup(e => e.ExtractSalarySlipDataAsync("salary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalarySlipDataDto(50000m, 24, "Full-Time", "Synthetic employer"));
        extraction.Setup(e => e.ExtractCribReportDataAsync("crib", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CribReportDataDto("A1", null, false, 0));
        var repository = new Mock<IFinancialFeasibilityRepository>();
        var handler = new AssessFinancialFeasibilityCommandHandler(extraction.Object,
            new FinancialFeasibilityScoringEngine(), repository.Object, TimeProvider.System,
            Mock.Of<ILogger<AssessFinancialFeasibilityCommandHandler>>());
        var result = await handler.HandleAsync(new AssessFinancialFeasibilityCommand(
            "case", "applicant", 15000.01m, 45000m, 1m, "bank", "salary", "crib", 100000m));
        Assert.Equal(100000m, result.PaymentCapacity!.VerifiedMonthlyIncomeLkr);
        Assert.Equal(15000m, result.PaymentCapacity.AvailableMonthlyLeasePaymentLkr);
        Assert.True(result.RequiresEscalation);
        repository.Verify(r => r.AddAsync(It.Is<FinancialFeasibilityAssessment>(a =>
            a.PaymentCapacity != null && !a.PaymentCapacity.IsWithinLimit), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public TestTimeProvider(DateTimeOffset time) => _time = time;
        public override DateTimeOffset GetUtcNow() => _time;
    }

    [Theory]
    [InlineData("B")]
    [InlineData("B1")]
    [InlineData("B3")]
    public async Task HandleAsync_ValidInput_PreservesIdsTypedAmountsAndTimeProviderTimestamp(string bureauGrade)
    {
        var fixedTime = new DateTimeOffset(2026, 9, 22, 12, 15, 0, TimeSpan.FromHours(5.5));
        var extractionMock = new Mock<IDocumentExtractionService>();

        extractionMock.Setup(e => e.ExtractBankStatementDataAsync("mock-statement.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankStatementDataDto(100_000m, 40_000m, 4, 0.2m));
        extractionMock.Setup(e => e.ExtractSalarySlipDataAsync("mock-salary1.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalarySlipDataDto(100_000m, 24, "Full-Time", "Acme Corp"));
        extractionMock.Setup(e => e.ExtractCribReportDataAsync("mock-crib.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CribReportDataDto(bureauGrade, 900_000m, false, 1));

        var breakdown = new FeasibilityScoreBreakdown(
            debtServiceRatio: 0.28m,
            liquidityBufferMonths: 4m,
            debtServiceRatioScore: 25m,
            incomeConsistencyScore: 25m,
            liquidityBufferScore: 15m,
            creditHistoryScore: 15m,
            penaltyScore: -15m,
            totalScore: 65m);
        var assessment = new FinancialFeasibilityAssessment(
            "LEASE-123",
            "PERSON-001",
            LeaseFeasibilityScoringContract.Component2V1.Version,
            FeasibilityGrade.C,
            FeasibilityAction.ManualReview,
            breakdown,
            fixedTime);

        var scoringEngineMock = new Mock<IFinancialFeasibilityScoringEngine>();
        scoringEngineMock
            .Setup(e => e.EvaluateFeasibility(
                It.IsAny<FinancialFeasibilityScoringInput>(),
                It.IsAny<DateTimeOffset>()))
            .Returns(assessment);

        var repoMock = new Mock<IFinancialFeasibilityRepository>();
        var handler = new AssessFinancialFeasibilityCommandHandler(
            extractionMock.Object,
            scoringEngineMock.Object,
            repoMock.Object,
            new TestTimeProvider(fixedTime),
            Mock.Of<ILogger<AssessFinancialFeasibilityCommandHandler>>());
        var command = new AssessFinancialFeasibilityCommand(
            ApplicationId: "LEASE-123",
            ApplicantId: "PERSON-001",
            RequestedMonthlyLeasePaymentLkr: 10_000m,
            MonthlyDebtObligationsLkr: 18_000m,
            IncomeConsistencyRatio: 1m,
            BankStatementUri: "mock-statement.txt",
            SalarySlipUri: "mock-salary1.txt",
            CribReportUri: "mock-crib.txt");

        var result = await handler.HandleAsync(command);

        Assert.Equal("LEASE-123", result.ApplicationId);
        Assert.Equal("PERSON-001", result.ApplicantId);
        Assert.Equal(65m, result.TotalScore);
        Assert.Equal("C", result.EligibilityGrade);
        Assert.Equal("ManualReview", result.RecommendedAction);
        Assert.True(result.RequiresManualReview);
        Assert.False(result.RequiresEscalation);
        Assert.Equal(fixedTime.ToUniversalTime(), result.EvaluationTimestamp);

        scoringEngineMock.Verify(e => e.EvaluateFeasibility(
            It.Is<FinancialFeasibilityScoringInput>(input =>
                input.ApplicationId == "LEASE-123" &&
                input.ApplicantId == "PERSON-001" &&
                input.RequestedMonthlyLeasePaymentLkr == 10_000m &&
                input.MonthlyDebtObligationsLkr == 18_000m &&
                input.IncomeConsistencyRatio == 1m),
            fixedTime), Times.Once);
        repoMock.Verify(r => r.AddAsync(
            It.Is<FinancialFeasibilityAssessment>(a =>
                a.ApplicationId == "LEASE-123" && a.ApplicantId == "PERSON-001"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
