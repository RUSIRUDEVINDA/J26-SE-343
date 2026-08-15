using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceRiskEngineTests
{
    private readonly GovernanceRiskEngine _engine = new();
    private readonly DateTime _baseTime = new(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AssessRisk_CleanInput_ReturnsLowRisk()
    {
        // Arrange
        var input = new GovernanceRiskEvaluationInput(
            "SUBJ-101",
            Array.Empty<DecisionHistoryObservation>(),
            Array.Empty<ComplianceViolationEvidence>(),
            Array.Empty<GovernanceConflictEvidence>(),
            Array.Empty<ComplaintObservation>(),
            Array.Empty<InstitutionalValidationObservation>()
        );

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal("SUBJ-101", result.SubjectId);
        Assert.Equal(0, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Low, result.Severity);
        Assert.False(result.RequiresHumanReview);
        Assert.Empty(result.TriggeredIndicators);
    }

    [Fact]
    public void AssessRisk_ApprovalPatternAnomaly_TriggersIndicator()
    {
        // Arrange: 3 approvals within 2 hours
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime, false, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime.AddHours(1), false, false),
            new("DEC-03", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime.AddHours(2), false, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.ApprovalPatternAnomaly);
        Assert.True(result.OverallRiskScore > 0);
    }

    [Fact]
    public void AssessRisk_RepeatedOverrideRisk_TriggersIndicator()
    {
        // Arrange: 2 override decisions
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(5), true, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.RepeatedOverrideRisk);
        Assert.Equal(25, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Moderate, result.Severity);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_RegulatoryViolationPattern_TriggersIndicator()
    {
        // Arrange: 2 violations
        var violations = new List<ComplianceViolationEvidence>
        {
            new("VIOL-01", "SUBJ-101", "RULE-1", "ZoningMismatch", _baseTime),
            new("VIOL-02", "SUBJ-101", "RULE-2", "DurationExceeded", _baseTime.AddDays(1))
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", null, violations, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.RegulatoryViolationPattern);
    }

    [Fact]
    public void AssessRisk_ConflictRecurrenceRisk_TriggersIndicator()
    {
        // Arrange: 2 governance conflicts
        var conflicts = new List<GovernanceConflictEvidence>
        {
            new("CONF-01", "SUBJ-101", "JurisdictionalOverlap", "Medium", _baseTime),
            new("CONF-02", "SUBJ-101", "ZoningIncompatibility", "High", _baseTime.AddHours(2))
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", null, null, conflicts, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.ConflictRecurrenceRisk);
    }

    [Fact]
    public void AssessRisk_ComplaintRiskIndicator_TriggersIndicator()
    {
        // Arrange: 1 verified complaint
        var complaints = new List<ComplaintObservation>
        {
            new("COMP-01", "SUBJ-101", _baseTime, "ProceduralIrregularity", "Medium", "Verified")
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", null, null, null, complaints, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.ComplaintRiskIndicator);
    }

    [Fact]
    public void AssessRisk_InstitutionalValidationRisk_TriggersIndicator()
    {
        // Arrange: 2 failed validations
        var validations = new List<InstitutionalValidationObservation>
        {
            new("VAL-01", "SUBJ-101", "DEPT-ENV", false, "EIA Missing", _baseTime),
            new("VAL-02", "SUBJ-101", "DEPT-URBAN", false, "Density Exceeded", _baseTime.AddDays(1))
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", null, null, null, null, validations);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.InstitutionalValidationRisk);
    }

    [Fact]
    public void AssessRisk_TemporalActivityAnomaly_TriggersIndicator()
    {
        // Arrange: 2 off-hours decisions (02:00 UTC and 03:00 UTC)
        var offHours1 = new DateTime(2026, 8, 13, 2, 0, 0, DateTimeKind.Utc);
        var offHours2 = new DateTime(2026, 8, 13, 3, 0, 0, DateTimeKind.Utc);

        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Approval", offHours1, false, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Approval", offHours2, false, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.TemporalActivityAnomaly);
    }

    [Fact]
    public void AssessRisk_DecisionConcentrationRisk_TriggersIndicator()
    {
        // Arrange: 4 decisions by same officer
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFFICER-XYZ", "INST-1", "Approval", _baseTime, false, false),
            new("DEC-02", "SUBJ-101", "OFFICER-XYZ", "INST-1", "Approval", _baseTime.AddDays(1), false, false),
            new("DEC-03", "SUBJ-101", "OFFICER-XYZ", "INST-1", "Approval", _baseTime.AddDays(2), false, false),
            new("DEC-04", "SUBJ-101", "OFFICER-XYZ", "INST-1", "Approval", _baseTime.AddDays(3), false, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.DecisionConcentrationRisk);
    }

    [Fact]
    public void AssessRisk_ExceptionFrequencyRisk_TriggersIndicator()
    {
        // Arrange: 2 discretionary exception decisions
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Exception", _baseTime, false, true),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Exception", _baseTime.AddDays(1), false, true)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.ExceptionFrequencyRisk);
    }

    [Fact]
    public void AssessRisk_CrossEvidenceRisk_AppliesCompoundBonus()
    {
        // Arrange: Trigger 3 distinct risk categories (Overrides, Violations, Complaints)
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };
        var violations = new List<ComplianceViolationEvidence>
        {
            new("VIOL-01", "SUBJ-101", "RULE-1", "Category", _baseTime),
            new("VIOL-02", "SUBJ-101", "RULE-2", "Category", _baseTime)
        };
        var complaints = new List<ComplaintObservation>
        {
            new("COMP-01", "SUBJ-101", _baseTime, "Procedural", "High", "Verified")
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, violations, null, complaints, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Contains(result.TriggeredIndicators, i => i.Category == GovernanceRiskCategory.CrossEvidenceRisk);
        // Base: Overrides(25) + Violations(20) + Complaints(15) + CompoundBonus(15) = 75
        Assert.Equal(75, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Critical, result.Severity);
    }

    [Fact]
    public void AssessRisk_ScoreBoundedAt100_CapsScore()
    {
        // Arrange: Multiple high-weight indicators that sum over 100
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, true),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddMinutes(10), true, true),
            new("DEC-03", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime.AddMinutes(20), false, false)
        };
        var violations = new List<ComplianceViolationEvidence>
        {
            new("V-01", "SUBJ-101", "R1", "Cat", _baseTime),
            new("V-02", "SUBJ-101", "R2", "Cat", _baseTime)
        };
        var conflicts = new List<GovernanceConflictEvidence>
        {
            new("C-01", "SUBJ-101", "Type", "Critical", _baseTime)
        };
        var complaints = new List<ComplaintObservation>
        {
            new("CMP-01", "SUBJ-101", _baseTime, "Cat", "High", "Verified")
        };
        var validations = new List<InstitutionalValidationObservation>
        {
            new("VAL-01", "SUBJ-101", "I1", false, "Reason", _baseTime),
            new("VAL-02", "SUBJ-101", "I2", false, "Reason", _baseTime)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, violations, conflicts, complaints, validations);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(100, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Critical, result.Severity);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_ScoreBoundaryAt24_ReturnsLowSeverity()
    {
        // Custom engine options: RapidApprovalWeight = 24
        var options = new GovernanceRiskEngineOptions(RapidApprovalMinCount: 3, RapidApprovalWeight: 24);
        var engine = new GovernanceRiskEngine(options);

        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime, false, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime.AddHours(1), false, false),
            new("DEC-03", "SUBJ-101", "OFF-1", "INST-1", "Approval", _baseTime.AddHours(2), false, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(24, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Low, result.Severity);
        Assert.False(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_ScoreBoundaryAt25_ReturnsModerateSeverity()
    {
        // 2 overrides = 25 weight
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(25, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Moderate, result.Severity);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_ScoreBoundaryAt50_ReturnsHighSeverity()
    {
        // Custom options: RepeatedOverrideWeight = 50
        var options = new GovernanceRiskEngineOptions(RepeatedOverrideWeight: 50);
        var engine = new GovernanceRiskEngine(options);

        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(50, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.High, result.Severity);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_ScoreBoundaryAt75_ReturnsCriticalSeverity()
    {
        // Custom options: RepeatedOverrideWeight = 75
        var options = new GovernanceRiskEngineOptions(RepeatedOverrideWeight: 75);
        var engine = new GovernanceRiskEngine(options);

        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(75, result.OverallRiskScore);
        Assert.Equal(GovernanceRiskSeverity.Critical, result.Severity);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public void AssessRisk_MissingOptionalCollections_HandledSafely()
    {
        // Arrange: All optional evidence lists null
        var input = new GovernanceRiskEvaluationInput("SUBJ-101", null, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal("SUBJ-101", result.SubjectId);
        Assert.Equal(0, result.OverallRiskScore);
        Assert.Empty(result.TriggeredIndicators);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssessRisk_NullOrWhitespaceSubjectId_ThrowsArgumentException(string invalidSubjectId)
    {
        // Arrange
        var input = new GovernanceRiskEvaluationInput(invalidSubjectId, null, null, null, null, null);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _engine.AssessRisk(input, _baseTime));
    }

    [Fact]
    public void AssessRisk_EmptyObservations_ReturnsZeroScore()
    {
        // Arrange
        var input = new GovernanceRiskEvaluationInput("SUBJ-101", new List<DecisionHistoryObservation>(), null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(0, result.OverallRiskScore);
        Assert.Empty(result.TriggeredIndicators);
    }

    [Fact]
    public void AssessRisk_DuplicateObservations_Deduplicated()
    {
        // Arrange: 2 duplicate override decision IDs
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false) // Duplicate
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert: Single override does not meet RepeatedOverrideMinCount (2)
        Assert.Equal(0, result.OverallRiskScore);
        Assert.Empty(result.TriggeredIndicators);
    }

    [Fact]
    public void AssessRisk_DeterministicExecution_SameInputProducesIdenticalOutput()
    {
        // Arrange
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };
        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result1 = _engine.AssessRisk(input, _baseTime);
        var result2 = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.Equal(result1.OverallRiskScore, result2.OverallRiskScore);
        Assert.Equal(result1.Severity, result2.Severity);
        Assert.Equal(result1.TriggeredIndicators.Count, result2.TriggeredIndicators.Count);
        Assert.Equal(result1.TriggeredIndicators[0].IndicatorId, result2.TriggeredIndicators[0].IndicatorId);
    }

    [Fact]
    public void AssessRisk_ReversedInputOrder_ProducesIdenticalResult()
    {
        // Arrange
        var d1 = new DecisionHistoryObservation("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false);
        var d2 = new DecisionHistoryObservation("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false);

        var inputForward = new GovernanceRiskEvaluationInput("SUBJ-101", new[] { d1, d2 }, null, null, null, null);
        var inputReversed = new GovernanceRiskEvaluationInput("SUBJ-101", new[] { d2, d1 }, null, null, null, null);

        // Act
        var resultForward = _engine.AssessRisk(inputForward, _baseTime);
        var resultReversed = _engine.AssessRisk(inputReversed, _baseTime);

        // Assert
        Assert.Equal(resultForward.OverallRiskScore, resultReversed.OverallRiskScore);
        Assert.Equal(resultForward.TriggeredIndicators[0].IndicatorId, resultReversed.TriggeredIndicators[0].IndicatorId);
    }

    [Fact]
    public void AssessRisk_IndicatorOrdering_SortedBySeverityAndCategory()
    {
        // Arrange: Trigger Overrides (25) and Complaints (15)
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };
        var complaints = new List<ComplaintObservation>
        {
            new("COMP-01", "SUBJ-101", _baseTime, "Category", "High", "Verified")
        };

        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, complaints, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert: RepeatedOverrideRisk (25) should appear before ComplaintRiskIndicator (15)
        Assert.Equal(2, result.TriggeredIndicators.Count);
        Assert.Equal(GovernanceRiskCategory.RepeatedOverrideRisk, result.TriggeredIndicators[0].Category);
        Assert.Equal(GovernanceRiskCategory.ComplaintRiskIndicator, result.TriggeredIndicators[1].Category);
    }

    [Fact]
    public void AssessRisk_ExplainabilityText_ContainsNonAccusatoryLanguage()
    {
        // Arrange
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };
        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);
        var indicator = result.TriggeredIndicators[0];

        // Assert
        Assert.DoesNotContain("corrupt", indicator.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fraud", indicator.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("human review", indicator.RecommendedAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssessRisk_HumanReviewRecommendation_ReturnedForElevatedRisk()
    {
        // Arrange
        var decisions = new List<DecisionHistoryObservation>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _baseTime.AddHours(1), true, false)
        };
        var input = new GovernanceRiskEvaluationInput("SUBJ-101", decisions, null, null, null, null);

        // Act
        var result = _engine.AssessRisk(input, _baseTime);

        // Assert
        Assert.True(result.RequiresHumanReview);
        Assert.Contains("Audit Committee", result.TriggeredIndicators[0].RecommendedAction);
    }
}
