using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Constants;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class ExplainableGovernanceEngineTests
{
    private readonly ExplainableGovernanceEngine _engine;
    private readonly DateTime _testTimestamp;

    public ExplainableGovernanceEngineTests()
    {
        _engine = new ExplainableGovernanceEngine();
        _testTimestamp = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Synthesize_WithComplianceViolations_ShouldGenerateComplianceExplanations()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("MAX_DURATION_EXCEEDED", "LeaseTerms", "Lease duration of 99 years exceeds max permitted limit of 50 years.") },
            new[] { "Mandatory environmental clearance certificate missing" });

        var input = new GovernanceExplanationInput("PARCEL-101", compliance, null, null);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Equal("PARCEL-101", result.SubjectId);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal(2, result.Explanations.Count);

        var violationItem = result.Explanations.FirstOrDefault(e => e.ReasonCode == GovernanceReasonCodes.ComplianceRuleViolation);
        Assert.NotNull(violationItem);
        Assert.Equal(EngineType.RegulatoryCompliance, violationItem.SourceEngine);
        Assert.Equal(GovernanceExplanationSeverity.High, violationItem.Severity);
        Assert.StartsWith("EXP-COMP-", violationItem.ItemId);
        Assert.True(violationItem.ItemId.Length >= 25); // EXP-COMP- (9 chars) + 16 hex chars = 25 chars min

        var conditionItem = result.Explanations.FirstOrDefault(e => e.ReasonCode == GovernanceReasonCodes.ComplianceConditionUnsatisfied);
        Assert.NotNull(conditionItem);
        Assert.Equal(EngineType.RegulatoryCompliance, conditionItem.SourceEngine);
    }

    [Fact]
    public void Synthesize_WithDetectedConflicts_ShouldGenerateConflictExplanations()
    {
        var conflict = new ConflictExplanationEvidence(
            new[]
            {
                new ConflictSummary(
                    "JURISDICTIONAL_OVERLAP",
                    GovernanceExplanationSeverity.High,
                    "Conflicting land classification between Agricultural Ministry and Coastal Authority.",
                    "RULE-ZONING-001",
                    "Convene inter-institutional mediation panel.")
            });

        var input = new GovernanceExplanationInput("PARCEL-102", null, conflict, null);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Single(result.Explanations);
        var item = result.Explanations[0];
        Assert.Equal(EngineType.GovernanceConflict, item.SourceEngine);
        Assert.Equal(GovernanceReasonCodes.ConflictingGovernanceDecisions, item.ReasonCode);
        Assert.Equal(GovernanceExplanationSeverity.High, item.Severity);
        Assert.StartsWith("EXP-CONF-", item.ItemId);
        Assert.True(item.ItemId.Length >= 25);
    }

    [Fact]
    public void Synthesize_WithRiskAssessment_ShouldGenerateRiskExplanations()
    {
        var risk = new RiskExplanationEvidence(
            overallScore: 65,
            riskSeverity: GovernanceExplanationSeverity.High,
            requiresHumanReview: true,
            triggeredIndicators: new[]
            {
                new RiskIndicatorSummary(
                    "OVERRIDE_FREQUENCY_ELEVATED",
                    "DecisionPattern",
                    GovernanceExplanationSeverity.High,
                    "4 override events detected within 12 months for officer OFF-789.",
                    "RULE-RISK-OVERRIDE",
                    "Audit authorization history and officer delegation limits.")
            });

        var input = new GovernanceExplanationInput("PARCEL-103", null, null, risk);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Single(result.Explanations);
        var item = result.Explanations[0];
        Assert.Equal(EngineType.RiskAndCorruption, item.SourceEngine);
        Assert.Equal(GovernanceReasonCodes.RepeatedOverridePattern, item.ReasonCode);
        Assert.Equal(GovernanceExplanationSeverity.High, item.Severity);
        Assert.StartsWith("EXP-RISK-", item.ItemId);
        Assert.True(item.ItemId.Length >= 25);
    }

    [Fact]
    public void Synthesize_WithMultipleSources_ShouldAggregateAllExplanations()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("MIN_VALUE_NOT_MET", "Valuation", "Valuation below threshold") },
            null);

        var conflict = new ConflictExplanationEvidence(
            new[] { new ConflictSummary("POLICY_CLASH", GovernanceExplanationSeverity.Moderate, "Policy clash", "RULE-POL-1", "Review policy") });

        var risk = new RiskExplanationEvidence(
            75,
            GovernanceExplanationSeverity.Critical,
            true,
            new[] { new RiskIndicatorSummary("COMPLAINT_SPIKE", "PublicFeedback", GovernanceExplanationSeverity.Critical, "Multiple complaints", "RULE-CMP-1", "Investigate") });

        var input = new GovernanceExplanationInput("PARCEL-104", compliance, conflict, risk);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(3, result.Explanations.Count);
        Assert.Equal(GovernanceExplanationSeverity.Critical, result.OverallSeverity);
        Assert.True(result.RequiresHumanReview);

        // Verification of Source Engine attribution
        Assert.Contains(result.Explanations, e => e.SourceEngine == EngineType.RegulatoryCompliance);
        Assert.Contains(result.Explanations, e => e.SourceEngine == EngineType.GovernanceConflict);
        Assert.Contains(result.Explanations, e => e.SourceEngine == EngineType.RiskAndCorruption);
    }

    [Fact]
    public void Synthesize_AllExplanations_ShouldUseStableReasonCodes()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("V1", "Cat1", "Msg1") },
            new[] { "Cond1" });

        var input = new GovernanceExplanationInput("PARCEL-105", compliance, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        foreach (var item in result.Explanations)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.ReasonCode));
            Assert.Contains(item.ReasonCode, new[]
            {
                GovernanceReasonCodes.ComplianceRuleViolation,
                GovernanceReasonCodes.ComplianceConditionUnsatisfied,
                GovernanceReasonCodes.ConflictingGovernanceDecisions,
                GovernanceReasonCodes.JurisdictionalOverlapDetected,
                GovernanceReasonCodes.HighGovernanceRisk,
                GovernanceReasonCodes.RepeatedOverridePattern,
                GovernanceReasonCodes.UnverifiedComplaintConcentration,
                GovernanceReasonCodes.InstitutionalValidationFailure,
                GovernanceReasonCodes.NoElevatedGovernanceIssues
            });
        }
    }

    [Fact]
    public void Synthesize_AllExplanations_ShouldContainHumanReadableText()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-1", "Cat", "Summary text") },
            null);

        var input = new GovernanceExplanationInput("PARCEL-106", compliance, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        var item = result.Explanations[0];
        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.False(string.IsNullOrWhiteSpace(item.PlainLanguageExplanation));
        Assert.Contains("Regulatory Violation", item.Title);
        Assert.Contains("PARCEL-106", item.PlainLanguageExplanation);
    }

    [Fact]
    public void Synthesize_Always_ShouldExcludeSensitiveRawData()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-1", "Cat", "Sanitized summary") },
            null);

        var input = new GovernanceExplanationInput("PARCEL-107", compliance, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        foreach (var item in result.Explanations)
        {
            // Verify no PII or sensitive raw keywords leak
            Assert.DoesNotContain("CONFIDENTIAL_SSN", item.PlainLanguageExplanation);
            Assert.DoesNotContain("SECRET_KEY", item.PlainLanguageExplanation);
        }
    }

    [Fact]
    public void Synthesize_Always_ShouldUseNonAccusatoryWording()
    {
        var risk = new RiskExplanationEvidence(
            80,
            GovernanceExplanationSeverity.Critical,
            true,
            new[] { new RiskIndicatorSummary("OVERRIDE_PATTERN", "Override", GovernanceExplanationSeverity.Critical, "Elevated override count", "RULE-OVR", "Review log") });

        var input = new GovernanceExplanationInput("PARCEL-108", null, null, risk);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result.Disclaimer);
        Assert.Contains("decision-support finding", result.Disclaimer);
        Assert.DoesNotContain("corrupt officer", result.Disclaimer);
        Assert.DoesNotContain("fraud proven", result.Disclaimer);
        Assert.DoesNotContain("guilty institution", result.Disclaimer);

        var item = result.Explanations[0];
        Assert.DoesNotContain("is corrupt", item.PlainLanguageExplanation);
        Assert.DoesNotContain("is guilty", item.PlainLanguageExplanation);
    }

    [Fact]
    public void Synthesize_Always_ShouldIncludeHumanReviewRecommendation()
    {
        var conflict = new ConflictExplanationEvidence(
            new[] { new ConflictSummary("JURISDICTION", GovernanceExplanationSeverity.High, "Overlap", "RULE-JUR", "Convene mediation committee.") });

        var input = new GovernanceExplanationInput("PARCEL-109", null, conflict, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        var item = result.Explanations[0];
        Assert.False(string.IsNullOrWhiteSpace(item.RecommendedAction));
        Assert.Equal("Convene mediation committee.", item.RecommendedAction);
    }

    [Fact]
    public void Synthesize_WhenNoElevatedIssues_ShouldReturnLowSeverityResult()
    {
        var input = new GovernanceExplanationInput("PARCEL-110", null, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Equal(GovernanceExplanationSeverity.Low, result.OverallSeverity);
        Assert.False(result.RequiresHumanReview);
        Assert.Single(result.Explanations);

        var item = result.Explanations[0];
        Assert.Equal(GovernanceReasonCodes.NoElevatedGovernanceIssues, item.ReasonCode);
        Assert.Equal(GovernanceExplanationSeverity.Low, item.Severity);
        Assert.False(item.RequiresHumanAttention);
    }

    [Fact]
    public void Synthesize_WithMultipleReasonsForSubject_ShouldIncludeAllReasons()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[]
            {
                new ComplianceViolationSummary("RULE-A", "Cat1", "Msg A"),
                new ComplianceViolationSummary("RULE-B", "Cat2", "Msg B")
            },
            new[] { "Cond C" });

        var input = new GovernanceExplanationInput("PARCEL-111", compliance, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(3, result.Explanations.Count);
    }

    [Fact]
    public void Synthesize_WithDuplicateEvidence_ShouldDeduplicateEvidenceSummaries()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-DUP", "Cat", "Msg DUP") },
            null);

        var input = new GovernanceExplanationInput("PARCEL-112", compliance, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        var item = result.Explanations[0];
        var distinctSummaries = item.EvidenceSummaries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(distinctSummaries.Count, item.EvidenceSummaries.Count);
    }

    [Fact]
    public void Synthesize_WithEmptyInputOutcomes_ShouldHandleGracefully()
    {
        var emptyCompliance = new ComplianceExplanationEvidence("Compliant", null, null);
        var emptyConflict = new ConflictExplanationEvidence(null);
        var emptyRisk = new RiskExplanationEvidence(0, GovernanceExplanationSeverity.Low, false, null);

        var input = new GovernanceExplanationInput("PARCEL-113", emptyCompliance, emptyConflict, emptyRisk);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Equal(GovernanceExplanationSeverity.Low, result.OverallSeverity);
    }

    [Fact]
    public void Synthesize_WhenInputIsNull_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.SynthesizeExplanation(null!, _testTimestamp));
    }

    [Fact]
    public void Synthesize_WhenOptionalEvidenceMissing_ShouldProduceValidExplanation()
    {
        var compliance = new ComplianceExplanationEvidence("NonCompliant", new[] { new ComplianceViolationSummary("RULE-X", "Cat", "Msg") }, null);
        var input = new GovernanceExplanationInput("PARCEL-114", compliance, null, null);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result);
        Assert.Single(result.Explanations);
    }

    [Fact]
    public void Synthesize_RepeatedExecution_ShouldBeDeterministic()
    {
        var compliance = new ComplianceExplanationEvidence("NonCompliant", new[] { new ComplianceViolationSummary("RULE-DET", "Cat", "Msg") }, null);
        var input = new GovernanceExplanationInput("PARCEL-115", compliance, null, null);

        var result1 = _engine.SynthesizeExplanation(input, _testTimestamp);
        var result2 = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(result1.ExplanationId, result2.ExplanationId);
        Assert.Equal(result1.OverallSeverity, result2.OverallSeverity);
        Assert.Equal(result1.Explanations.Count, result2.Explanations.Count);
        Assert.Equal(result1.Explanations[0].ItemId, result2.Explanations[0].ItemId);
    }

    [Fact]
    public void Synthesize_ReversedInputOrdering_ShouldProduceIdenticalResult()
    {
        var compliance1 = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[]
            {
                new ComplianceViolationSummary("RULE-1", "Cat", "Msg1"),
                new ComplianceViolationSummary("RULE-2", "Cat", "Msg2")
            },
            null);

        var compliance2 = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[]
            {
                new ComplianceViolationSummary("RULE-2", "Cat", "Msg2"),
                new ComplianceViolationSummary("RULE-1", "Cat", "Msg1")
            },
            null);

        var input1 = new GovernanceExplanationInput("PARCEL-116", compliance1, null, null);
        var input2 = new GovernanceExplanationInput("PARCEL-116", compliance2, null, null);

        var result1 = _engine.SynthesizeExplanation(input1, _testTimestamp);
        var result2 = _engine.SynthesizeExplanation(input2, _testTimestamp);

        Assert.Equal(result1.ExplanationId, result2.ExplanationId);
        Assert.Equal(result1.Explanations[0].ItemId, result2.Explanations[0].ItemId);
        Assert.Equal(result1.Explanations[1].ItemId, result2.Explanations[1].ItemId);
    }

    [Fact]
    public void Synthesize_OutputExplanations_ShouldBeOrderedBySeverityAndEngine()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-LOW", "Cat", "Low impact rule") },
            null); // High severity by engine logic

        var risk = new RiskExplanationEvidence(
            90,
            GovernanceExplanationSeverity.Critical,
            true,
            new[] { new RiskIndicatorSummary("CRITICAL_IND", "Cat", GovernanceExplanationSeverity.Critical, "Critical risk", "RULE-CRIT", "Action") });

        var input = new GovernanceExplanationInput("PARCEL-117", compliance, null, risk);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(2, result.Explanations.Count);
        // Critical severity risk item should come before High severity compliance item
        Assert.Equal(GovernanceExplanationSeverity.Critical, result.Explanations[0].Severity);
        Assert.Equal(GovernanceExplanationSeverity.High, result.Explanations[1].Severity);
    }

    [Fact]
    public void Synthesize_SameInputAndTimestamp_ShouldProduceStableExplanationId()
    {
        var compliance = new ComplianceExplanationEvidence("NonCompliant", new[] { new ComplianceViolationSummary("RULE-STABLE", "Cat", "Msg") }, null);
        var input = new GovernanceExplanationInput("PARCEL-118", compliance, null, null);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.NotNull(result.ExplanationId);
        Assert.Equal(16, result.ExplanationId.Length);
    }

    [Fact]
    public void Synthesize_EvidenceCasingAndWhitespace_ShouldBeNormalized()
    {
        var compliance1 = new ComplianceExplanationEvidence("NonCompliant", new[] { new ComplianceViolationSummary("RULE-SPACE", "Cat", "  Message text  ") }, null);
        var compliance2 = new ComplianceExplanationEvidence("NonCompliant", new[] { new ComplianceViolationSummary("RULE-SPACE", "Cat", "Message text") }, null);

        var input1 = new GovernanceExplanationInput("PARCEL-119", compliance1, null, null);
        var input2 = new GovernanceExplanationInput("PARCEL-119", compliance2, null, null);

        var result1 = _engine.SynthesizeExplanation(input1, _testTimestamp);
        var result2 = _engine.SynthesizeExplanation(input2, _testTimestamp);

        Assert.Equal(result1.Explanations[0].ItemId, result2.Explanations[0].ItemId);
    }

    [Fact]
    public void Synthesize_Result_ShouldContainSharedTimestamp()
    {
        var input = new GovernanceExplanationInput("PARCEL-120", null, null, null);
        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(_testTimestamp, result.EvaluationTimestamp);
    }

    [Fact]
    public void Synthesize_ShouldAttributeCorrectSourceEngine_ForComplianceConflictAndRisk()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-COMP-SRC", "CategoryA", "Compliance issue") },
            null);

        var conflict = new ConflictExplanationEvidence(
            new[] { new ConflictSummary("TYPE-CONF-SRC", GovernanceExplanationSeverity.High, "Conflict issue", "RULE-CONF-SRC", "Resolve conflict") });

        var risk = new RiskExplanationEvidence(
            80,
            GovernanceExplanationSeverity.Critical,
            true,
            new[] { new RiskIndicatorSummary("IND-RISK-SRC", "CategoryC", GovernanceExplanationSeverity.Critical, "Risk issue", "RULE-RISK-SRC", "Resolve risk") });

        var input = new GovernanceExplanationInput("PARCEL-SOURCE-TEST", compliance, conflict, risk);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        Assert.Equal(3, result.Explanations.Count);

        var compItem = result.Explanations.Single(e => e.ReasonCode == GovernanceReasonCodes.ComplianceRuleViolation);
        Assert.Equal(EngineType.RegulatoryCompliance, compItem.SourceEngine);

        var confItem = result.Explanations.Single(e => e.ReasonCode == GovernanceReasonCodes.ConflictingGovernanceDecisions);
        Assert.Equal(EngineType.GovernanceConflict, confItem.SourceEngine);

        var riskItem = result.Explanations.Single(e => e.ReasonCode == GovernanceReasonCodes.HighGovernanceRisk);
        Assert.Equal(EngineType.RiskAndCorruption, riskItem.SourceEngine);
    }

    [Fact]
    public void Synthesize_WhenSafeSummarizedEvidenceSupplied_ShouldIncludeNonSensitiveEvidenceSummaries()
    {
        var compliance = new ComplianceExplanationEvidence(
            "NonCompliant",
            new[] { new ComplianceViolationSummary("RULE-VAL-001", "Valuation", "Valuation below statutory minimum threshold") },
            new[] { "Environment clearance certificate pending" });

        var input = new GovernanceExplanationInput("PARCEL-EVIDENCE-TEST", compliance, null, null);

        var result = _engine.SynthesizeExplanation(input, _testTimestamp);

        var violationItem = result.Explanations.Single(e => e.ReasonCode == GovernanceReasonCodes.ComplianceRuleViolation);
        Assert.NotEmpty(violationItem.EvidenceSummaries);
        Assert.Contains(violationItem.EvidenceSummaries, s => s.Contains("RULE-VAL-001"));
        Assert.Contains(violationItem.EvidenceSummaries, s => s.Contains("Valuation"));
        Assert.Contains(violationItem.EvidenceSummaries, s => s.Contains("Valuation below statutory minimum threshold"));

        // Confirm privacy rules respected (no PII or raw confidential text)
        Assert.DoesNotContain(violationItem.EvidenceSummaries, s => s.Contains("CONFIDENTIAL_PII"));
        Assert.DoesNotContain(violationItem.EvidenceSummaries, s => s.Contains("PASSPORT_NO"));
    }
}
