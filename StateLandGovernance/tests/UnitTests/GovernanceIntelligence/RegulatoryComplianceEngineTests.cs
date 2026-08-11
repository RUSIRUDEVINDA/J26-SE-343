using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class RegulatoryComplianceEngineTests
{
    private readonly RegulatoryComplianceEngine _engine = new();

    [Fact]
    public void Evaluate_ShouldReturnCompliant_WhenValidInputsProvided()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 25,
            ProposedUse: "Conservation",
            LeaseAmount: 5000.00m,
            ZoningArea: "ForestReserve"
        );
        var rules = new List<RegulatoryRule>
        {
            new MaxLeaseDurationRule(),
            new ZoningMatchRule(),
            new MinimumLeaseValueRule()
        };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.Compliant, result.Status);
        Assert.Empty(result.Violations);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Evaluate_ShouldReturnNonCompliant_WhenLeaseDurationExceedsLimit()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 100, // Legal limit is 99 years
            ProposedUse: "Conservation",
            LeaseAmount: 5000.00m,
            ZoningArea: "ForestReserve"
        );
        var rules = new List<RegulatoryRule> { new MaxLeaseDurationRule() };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.NonCompliant, result.Status);
        Assert.Single(result.Violations);
        Assert.Equal("RULE_LEASE_MAX_DURATION", result.Violations[0].RuleCode);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Evaluate_ShouldReturnConditional_WhenLeaseDurationExceedsConditionalThreshold()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 45, // Threshold is 30 years
            ProposedUse: "Conservation",
            LeaseAmount: 5000.00m,
            ZoningArea: "ForestReserve"
        );
        var rules = new List<RegulatoryRule> { new MaxLeaseDurationRule() };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.Conditional, result.Status);
        Assert.Empty(result.Violations);
        Assert.Single(result.Conditions);
        Assert.Contains("ministerial review", result.Conditions[0].Description);
    }

    [Fact]
    public void Evaluate_ShouldReturnCompliant_WhenNoRulesProvided()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 150,
            ProposedUse: "Industrial",
            LeaseAmount: -100.00m,
            ZoningArea: "ForestReserve"
        );
        var rules = Enumerable.Empty<RegulatoryRule>();

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.Compliant, result.Status);
        Assert.Empty(result.Violations);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Evaluate_ShouldEvaluateMultipleRules()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 45,       // Conditional max duration
            ProposedUse: "EcoTourism",   // Compliant for forest reserve
            LeaseAmount: 500.00m,        // Conditional valuation check
            ZoningArea: "ForestReserve"
        );
        var rules = new List<RegulatoryRule>
        {
            new MaxLeaseDurationRule(),
            new ZoningMatchRule(),
            new MinimumLeaseValueRule()
        };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.Conditional, result.Status);
        Assert.Empty(result.Violations);
        Assert.Equal(2, result.Conditions.Count);
    }

    [Fact]
    public void Evaluate_ShouldCaptureMultipleViolations()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 150,     // Violation: > 99 years
            ProposedUse: "Industrial",   // Violation: Prohibited in Residential zone
            LeaseAmount: -50.00m,        // Violation: Negative amount
            ZoningArea: "Residential"
        );
        var rules = new List<RegulatoryRule>
        {
            new MaxLeaseDurationRule(),
            new ZoningMatchRule(),
            new MinimumLeaseValueRule()
        };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.NonCompliant, result.Status);
        Assert.Equal(3, result.Violations.Count);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Evaluate_ShouldThrowArgumentNullException_WhenInputIsNull()
    {
        // Arrange
        var rules = new List<RegulatoryRule> { new MaxLeaseDurationRule() };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _engine.Evaluate(null!, rules));
    }

    [Fact]
    public void Evaluate_ShouldThrowArgumentNullException_WhenRulesListIsNull()
    {
        // Arrange
        var input = new LeaseEvaluationInput(25, "Conservation", 1000m, "ForestReserve");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _engine.Evaluate(input, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Evaluate_ShouldFailBoundaryLeaseDuration_WhenDurationIsNonPositive(int invalidDuration)
    {
        // Arrange
        var input = new LeaseEvaluationInput(invalidDuration, "Conservation", 5000m, "ForestReserve");
        var rules = new List<RegulatoryRule> { new MaxLeaseDurationRule() };

        // Act
        var result = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(ComplianceStatus.NonCompliant, result.Status);
        Assert.Single(result.Violations);
        Assert.Contains("must be greater than zero", result.Violations[0].Message);
    }

    [Fact]
    public void Evaluate_ShouldBeDeterministicAndRepeatedIdentically()
    {
        // Arrange
        var input = new LeaseEvaluationInput(
            LeaseDurationYears: 45,
            ProposedUse: "Commercial",
            LeaseAmount: 500.00m,
            ZoningArea: "Residential"
        );
        var rules = new List<RegulatoryRule>
        {
            new MaxLeaseDurationRule(),
            new ZoningMatchRule(),
            new MinimumLeaseValueRule()
        };

        // Act
        var firstRun = _engine.Evaluate(input, rules);
        var secondRun = _engine.Evaluate(input, rules);

        // Assert
        Assert.Equal(firstRun.Status, secondRun.Status);
        Assert.Equal(firstRun.Violations.Count, secondRun.Violations.Count);
        Assert.Equal(firstRun.Conditions.Count, secondRun.Conditions.Count);

        for (int i = 0; i < firstRun.Conditions.Count; i++)
        {
            Assert.Equal(firstRun.Conditions[i].Description, secondRun.Conditions[i].Description);
        }
    }
}
