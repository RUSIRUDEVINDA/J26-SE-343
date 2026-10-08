using System;
using System.Linq;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Events;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class FinancialFeasibilityAssessmentTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 9, 22, 8, 30, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public void Constructor_WithValidArguments_CreatesAssessmentWithBothIdsAndProvidedTime()
    {
        var scoreBreakdown = CreateBreakdown();

        var assessment = CreateAssessment(scoreBreakdown);

        Assert.Equal("APP-123", assessment.ApplicationId);
        Assert.Equal("PERSON-456", assessment.ApplicantId);
        Assert.Equal("contract-v1", assessment.ContractVersion);
        Assert.Equal(FeasibilityGrade.B, assessment.Grade);
        Assert.Equal(FeasibilityAction.Proceed, assessment.Action);
        Assert.Equal(scoreBreakdown, assessment.ScoreBreakdown);
        Assert.Equal(GeneratedAt.ToUniversalTime(), assessment.GeneratedAt);
        Assert.False(assessment.IsFinalized);
        Assert.Empty(assessment.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidApplicationId_ThrowsArgumentException(string? invalidId)
    {
        Assert.Throws<ArgumentException>(() => new FinancialFeasibilityAssessment(
            invalidId!,
            "PERSON-456",
            "contract-v1",
            FeasibilityGrade.A,
            FeasibilityAction.FastTrack,
            CreateBreakdown(),
            GeneratedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidApplicantId_ThrowsArgumentException(string? invalidId)
    {
        Assert.Throws<ArgumentException>(() => new FinancialFeasibilityAssessment(
            "APP-123",
            invalidId!,
            "contract-v1",
            FeasibilityGrade.A,
            FeasibilityAction.FastTrack,
            CreateBreakdown(),
            GeneratedAt));
    }

    [Fact]
    public void Constructor_WithNullScoreBreakdown_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FinancialFeasibilityAssessment(
            "APP-123",
            "PERSON-456",
            "contract-v1",
            FeasibilityGrade.A,
            FeasibilityAction.FastTrack,
            null!,
            GeneratedAt));
    }

    [Fact]
    public void FinalizeAssessment_RaisesDomainEventAndSetsFlag()
    {
        var assessment = CreateAssessment(CreateBreakdown());

        assessment.FinalizeAssessment();

        Assert.True(assessment.IsFinalized);
        var domainEvent = assessment.DomainEvents.SingleOrDefault() as FinancialFeasibilityAssessmentFinalizedEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(assessment.Id, domainEvent.AssessmentId);
        Assert.Equal("APP-123", domainEvent.ApplicationId);
        Assert.Equal(FeasibilityGrade.B, domainEvent.Grade);
    }

    private static FinancialFeasibilityAssessment CreateAssessment(FeasibilityScoreBreakdown scoreBreakdown) =>
        new(
            "APP-123",
            "PERSON-456",
            "contract-v1",
            FeasibilityGrade.B,
            FeasibilityAction.Proceed,
            scoreBreakdown,
            GeneratedAt);

    private static FeasibilityScoreBreakdown CreateBreakdown() =>
        new(
            debtServiceRatio: 0.30m,
            liquidityBufferMonths: 4m,
            debtServiceRatioScore: 25m,
            incomeConsistencyScore: 20m,
            liquidityBufferScore: 15m,
            creditHistoryScore: 15m,
            penaltyScore: 0m,
            totalScore: 75m);
}
