using System;
using System.Linq;
using Xunit;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Events;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class FinancialFeasibilityAssessmentTests
{
    [Fact]
    public void Constructor_WithValidArguments_CreatesAssessment()
    {
        // Arrange
        var scoreBreakdown = new FeasibilityScoreBreakdown(15, 15, 20, 10, 20, 0, 80);
        
        // Act
        var assessment = new FinancialFeasibilityAssessment(
            "APP-123",
            FeasibilityGrade.B,
            scoreBreakdown);

        // Assert
        Assert.NotNull(assessment);
        Assert.Equal("APP-123", assessment.ApplicationId);
        Assert.Equal(FeasibilityGrade.B, assessment.Grade);
        Assert.Equal(scoreBreakdown, assessment.ScoreBreakdown);
        Assert.False(assessment.IsFinalized);
        Assert.Empty(assessment.DomainEvents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidApplicationId_ThrowsArgumentException(string invalidId)
    {
        // Arrange
        var scoreBreakdown = new FeasibilityScoreBreakdown(15, 15, 20, 10, 20, 0, 80);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FinancialFeasibilityAssessment(
            invalidId,
            FeasibilityGrade.A,
            scoreBreakdown));
    }

    [Fact]
    public void Constructor_WithNullScoreBreakdown_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FinancialFeasibilityAssessment(
            "APP-123",
            FeasibilityGrade.A,
            null!));
    }

    [Fact]
    public void FinalizeAssessment_RaisesDomainEventAndSetsFlag()
    {
        // Arrange
        var scoreBreakdown = new FeasibilityScoreBreakdown(15, 15, 20, 10, 20, 0, 80);
        var assessment = new FinancialFeasibilityAssessment("APP-123", FeasibilityGrade.B, scoreBreakdown);

        // Act
        assessment.FinalizeAssessment();

        // Assert
        Assert.True(assessment.IsFinalized);
        var domainEvent = assessment.DomainEvents.SingleOrDefault() as FinancialFeasibilityAssessmentFinalizedEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(assessment.Id, domainEvent.AssessmentId);
        Assert.Equal("APP-123", domainEvent.ApplicationId);
        Assert.Equal(FeasibilityGrade.B, domainEvent.Grade);
    }
}
