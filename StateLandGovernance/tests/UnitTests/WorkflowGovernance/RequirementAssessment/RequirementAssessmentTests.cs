namespace StateLandGovernance.UnitTests.WorkflowGovernance.RequirementAssessment;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;
using Xunit;

public class RequirementAssessmentTests
{
    private static readonly AssessmentPolicyId FictionalProposalPolicyId = new("FICTIONAL-POL-PROP-001");
    private static readonly AssessmentPolicyId FictionalCabinetPolicyId = new("FICTIONAL-POL-CABINET-001");
    private const string FictionalPolicyVersion = "2026.1-FICTIONAL";
    private const string FictionalSourceRef = "FICTIONAL-CIRCULAR-9999/TEST";

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly LeaseCaseId _leaseCaseId = new(Guid.NewGuid());
    private readonly DateTime _actionTime = DateTime.UtcNow;
    private readonly VerifiedAuthoritySnapshot _authority;

    public RequirementAssessmentTests()
    {
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString());
        _authority = new VerifiedAuthoritySnapshot(
            _actorId,
            new[] { "LeaseInitiator" },
            scope,
            _actionTime.AddMinutes(-10),
            _actionTime.AddMinutes(-5),
            _actionTime.AddMinutes(30));
    }

    private LeaseProposalIntake CreateValidFictionalIntake(
        decimal extentValue = 75.0m,
        MeasurementUnit? unit = null,
        LeasePurpose? purpose = null,
        JurisdictionContext? jurisdiction = null)
    {
        return new LeaseProposalIntake(
            purpose ?? LeasePurpose.Commercial,
            new LandExtent(extentValue, unit ?? MeasurementUnit.Acre),
            jurisdiction ?? new JurisdictionContext("PROV-WEST-FICTIONAL", "Region-Fictional", "District-Fictional"),
            new IntakeSourceReference("FICTIONAL-INTAKE-FORM", "v1.0"));
    }

    // 1. Valid proposal-requirement assessment.
    [Fact]
    public void Assess_ValidProposalRequirement_EvaluatesSuccessfully()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Fictional Proposal Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(_leaseCaseId, result.LeaseCaseId);
        Assert.Equal(AssessmentSubject.FormalProposal, result.Subject);
        Assert.Equal(RequirementAssessmentOutcome.Required, result.Outcome);
        Assert.Equal(FictionalProposalPolicyId.Value, result.MatchedPolicyId);
        Assert.Equal(FictionalPolicyVersion, result.MatchedPolicyVersion);
        Assert.False(result.RequiresHumanConfirmation);
        Assert.Contains("exceeds threshold", result.Explanation);
    }

    // 2. Valid higher-authority-prerequisite assessment.
    [Fact]
    public void Assess_ValidHigherAuthorityPrerequisite_EvaluatesSuccessfully()
    {
        var policy = new AssessmentPolicy(
            FictionalCabinetPolicyId,
            FictionalPolicyVersion,
            "Fictional Cabinet Prerequisite Policy",
            FictionalSourceRef,
            AssessmentSubject.HigherAuthorityPrerequisite,
            PolicyStatus.Active,
            extentThreshold: 100.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThanOrEqual);

        var intake = CreateValidFictionalIntake(extentValue: 120.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.HigherAuthorityPrerequisite, intake, policy, _actionTime);

        Assert.Equal(AssessmentSubject.HigherAuthorityPrerequisite, result.Subject);
        Assert.Equal(RequirementAssessmentOutcome.Required, result.Outcome);
        Assert.Equal(FictionalCabinetPolicyId.Value, result.MatchedPolicyId);
        Assert.False(result.RequiresHumanConfirmation);
    }

    // 3. Required result from a matching configurable rule.
    [Fact]
    public void Assess_MatchingConfigurableRule_ReturnsRequiredOutcome()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Fictional Threshold Rule",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 25.0m,
            thresholdUnit: MeasurementUnit.Hectare,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 30.0m, unit: MeasurementUnit.Hectare);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Required, result.Outcome);
        Assert.False(result.RequiresHumanConfirmation);
    }

    // 4. Not-required result from a confirmed rule.
    [Fact]
    public void Assess_ConfirmedRuleNotExceeded_ReturnsNotRequiredOutcome()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Fictional Threshold Rule",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 40.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.NotRequired, result.Outcome);
        Assert.False(result.RequiresHumanConfirmation);
        Assert.Contains("does not exceed threshold", result.Explanation);
    }

    // 5. Missing policy.
    [Fact]
    public void Assess_MissingPolicy_ReturnsUndeterminedWithExplanation()
    {
        var intake = CreateValidFictionalIntake();

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, null, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.Null(result.MatchedPolicyId);
        Assert.Null(result.MatchedPolicyVersion);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("No applicable or active policy was found", result.Explanation);
    }

    // 6. Inactive policy.
    [Fact]
    public void Assess_InactivePolicy_ReturnsHumanReviewRequiredOrUndetermined()
    {
        var expiredPeriod = new EffectivePeriod(_actionTime.AddDays(-20), _actionTime.AddDays(-10));
        var expiredPolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Expired Fictional Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: expiredPeriod,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 80.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, expiredPolicy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("not effective at the assessment timestamp", result.Explanation);
    }

    // 7. Draft or unconfirmed policy.
    [Fact]
    public void Assess_DraftOrUnconfirmedPolicy_ReturnsHumanReviewRequired()
    {
        var draftPolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Draft Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Draft,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var awaitingPolicy = new AssessmentPolicy(
            FictionalCabinetPolicyId,
            FictionalPolicyVersion,
            "Awaiting Confirmation Policy",
            FictionalSourceRef,
            AssessmentSubject.HigherAuthorityPrerequisite,
            PolicyStatus.AwaitingConfirmation,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 80.0m, unit: MeasurementUnit.Acre);

        var draftResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, draftPolicy, _actionTime);
        var awaitingResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.HigherAuthorityPrerequisite, intake, awaitingPolicy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, draftResult.Outcome);
        Assert.True(draftResult.RequiresHumanConfirmation);
        Assert.Contains("Draft status", draftResult.Explanation);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, awaitingResult.Outcome);
        Assert.True(awaitingResult.RequiresHumanConfirmation);
        Assert.Contains("awaiting confirmation", awaitingResult.Explanation);
    }

    // 8. Missing lease purpose.
    [Fact]
    public void Assess_MissingLeasePurpose_WhenRequiredByPolicy_ReturnsUndetermined()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Purpose Specific Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = new LeaseProposalIntake(
            purpose: null,
            requestedExtent: new LandExtent(60.0m, MeasurementUnit.Acre),
            jurisdiction: new JurisdictionContext("PROV-WEST"),
            sourceReference: new IntakeSourceReference("APP", "v1"));

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("missing required lease purpose", result.Explanation);
    }

    // 9. Missing requested extent.
    [Fact]
    public void Assess_MissingRequestedExtent_WhenThresholdEvaluated_ReturnsUndetermined()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Threshold Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = new LeaseProposalIntake(
            purpose: LeasePurpose.Commercial,
            requestedExtent: null,
            jurisdiction: new JurisdictionContext("PROV-WEST"),
            sourceReference: new IntakeSourceReference("APP", "v1"));

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("missing requested land extent", result.Explanation);
    }

    // 10. Invalid extent.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    public void LandExtent_NegativeOrZero_ThrowsInvalidLandExtentException(decimal invalidExtent)
    {
        var ex = Assert.Throws<InvalidLandExtentException>(() => new LandExtent(invalidExtent, MeasurementUnit.Acre));
        Assert.Contains("cannot be negative or zero", ex.Message);
    }

    // 11. Incompatible measurement unit.
    [Fact]
    public void Assess_IncompatibleMeasurementUnit_ReturnsHumanReviewRequiredWithoutSilentlyComparing()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Acre Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        // Case supplies extent in Hectares
        var intake = CreateValidFictionalIntake(extentValue: 50.0m, unit: MeasurementUnit.Hectare);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("Incompatible measurement units", result.Explanation);
        Assert.Contains("Hectare", result.Explanation);
        Assert.Contains("Acre", result.Explanation);
    }

    // 12. Confirmed greater-than comparison.
    [Fact]
    public void Assess_ConfirmedGreaterThanComparison_EvaluatesStrictInequality()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Strict GT Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan,
            isEqualityBoundaryConfirmed: true);

        // Above threshold -> Required
        var aboveIntake = CreateValidFictionalIntake(extentValue: 50.1m, unit: MeasurementUnit.Acre);
        var aboveResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, aboveIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.Required, aboveResult.Outcome);
        Assert.False(aboveResult.RequiresHumanConfirmation);

        // Equal to threshold -> NotRequired (strict GT)
        var equalIntake = CreateValidFictionalIntake(extentValue: 50.0m, unit: MeasurementUnit.Acre);
        var equalResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, equalIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, equalResult.Outcome);
        Assert.False(equalResult.RequiresHumanConfirmation);

        // Below threshold -> NotRequired
        var belowIntake = CreateValidFictionalIntake(extentValue: 49.9m, unit: MeasurementUnit.Acre);
        var belowResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, belowIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, belowResult.Outcome);
        Assert.False(belowResult.RequiresHumanConfirmation);
    }

    // 13. Confirmed greater-than-or-equal comparison.
    [Fact]
    public void Assess_ConfirmedGreaterThanOrEqualComparison_EvaluatesInclusiveInequality()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Inclusive GTE Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThanOrEqual,
            isEqualityBoundaryConfirmed: true);

        // Above threshold -> Required
        var aboveIntake = CreateValidFictionalIntake(extentValue: 55.0m, unit: MeasurementUnit.Acre);
        var aboveResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, aboveIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.Required, aboveResult.Outcome);

        // Equal to threshold -> Required (inclusive GTE)
        var equalIntake = CreateValidFictionalIntake(extentValue: 50.0m, unit: MeasurementUnit.Acre);
        var equalResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, equalIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.Required, equalResult.Outcome);
        Assert.False(equalResult.RequiresHumanConfirmation);

        // Below threshold -> NotRequired
        var belowIntake = CreateValidFictionalIntake(extentValue: 49.9m, unit: MeasurementUnit.Acre);
        var belowResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, belowIntake, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, belowResult.Outcome);
    }

    // 14. Equality where the boundary is unresolved.
    [Fact]
    public void Assess_EqualityWhereBoundaryIsUnresolved_ReturnsHumanReviewRequired()
    {
        var unresolvedBoundaryPolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Unresolved Boundary Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan,
            isEqualityBoundaryConfirmed: false);

        var equalIntake = CreateValidFictionalIntake(extentValue: 50.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, equalIntake, unresolvedBoundaryPolicy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("equality boundary has not been confirmed", result.Explanation);
    }

    // 15. Purpose mismatch does not produce NotRequired.
    [Fact]
    public void Assess_PurposeMismatch_DoesNotProduceNotRequired_ReturnsUndetermined()
    {
        var commercialPolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Commercial Only Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var commercialIntake = CreateValidFictionalIntake(extentValue: 60.0m, purpose: LeasePurpose.Commercial);
        var agriIntake = CreateValidFictionalIntake(extentValue: 60.0m, purpose: LeasePurpose.Agricultural);

        var matchingResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, commercialIntake, commercialPolicy, _actionTime);
        var nonMatchingResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, agriIntake, commercialPolicy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Required, matchingResult.Outcome);
        Assert.Equal(RequirementAssessmentOutcome.Undetermined, nonMatchingResult.Outcome);
        Assert.True(nonMatchingResult.RequiresHumanConfirmation);
        Assert.Contains("is not applicable to case purpose", nonMatchingResult.Explanation);
    }

    // 16. Jurisdiction mismatch does not produce NotRequired.
    [Fact]
    public void Assess_JurisdictionMismatch_DoesNotProduceNotRequired_ReturnsUndetermined()
    {
        var targetJurisdiction = new JurisdictionContext("JURIS-FICTIONAL-01");
        var otherJurisdiction = new JurisdictionContext("JURIS-FICTIONAL-02");

        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Jurisdiction Specific Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicableJurisdiction: targetJurisdiction,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var matchingIntake = CreateValidFictionalIntake(extentValue: 60.0m, jurisdiction: targetJurisdiction);
        var nonMatchingIntake = CreateValidFictionalIntake(extentValue: 60.0m, jurisdiction: otherJurisdiction);

        var matchingResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, matchingIntake, policy, _actionTime);
        var nonMatchingResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, nonMatchingIntake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Required, matchingResult.Outcome);
        Assert.Equal(RequirementAssessmentOutcome.Undetermined, nonMatchingResult.Outcome);
        Assert.True(nonMatchingResult.RequiresHumanConfirmation);
        Assert.Contains("is not applicable to case jurisdiction", nonMatchingResult.Explanation);
    }

    // 17. Policy-version traceability.
    [Fact]
    public void Assess_PolicyVersionTraceability_RetainsExactPolicyIdentifiers()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2026.V2-TRACED",
            "Traceable Policy",
            "GAZETTE-FICTIONAL-2026-09",
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 75.0m, unit: MeasurementUnit.Acre);

        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal("FICTIONAL-POL-PROP-001", result.MatchedPolicyId);
        Assert.Equal("2026.V2-TRACED", result.MatchedPolicyVersion);
        Assert.Equal("GAZETTE-FICTIONAL-2026-09", result.SourceReference);
        Assert.NotNull(result.EvaluatedInputs);
        Assert.Equal(75.0m, result.EvaluatedInputs.ExtentValue);
        Assert.Equal("Acre", result.EvaluatedInputs.ExtentUnit);
    }

    // 18. Deterministic repeated evaluation.
    [Fact]
    public void Assess_DeterministicRepeatedEvaluation_ProducesIdenticalOutputs()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Deterministic Test Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 55.5m, unit: MeasurementUnit.Acre);

        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);
        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(result1.Outcome, result2.Outcome);
        Assert.Equal(result1.MatchedPolicyId, result2.MatchedPolicyId);
        Assert.Equal(result1.MatchedPolicyVersion, result2.MatchedPolicyVersion);
        Assert.Equal(result1.Explanation, result2.Explanation);
        Assert.Equal(result1.RequiresHumanConfirmation, result2.RequiresHumanConfirmation);
        Assert.Equal(result1.AssessedAt, result2.AssessedAt);
    }

    // 19. Reassessment without overwriting earlier result.
    [Fact]
    public void RecordAssessment_Reassessment_AppendsNewResultWithoutOverwritingHistoricalResult()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);

        var policyV1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 100.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var policyV2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Policy v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 75.0m, unit: MeasurementUnit.Acre);

        var assessmentTime1 = _actionTime.AddMinutes(1);
        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV1, assessmentTime1);
        leaseCase.RecordAssessmentResult(result1);

        Assert.Single(leaseCase.AssessmentHistory);
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, leaseCase.AssessmentHistory.First().Outcome);
        Assert.Equal("1.0", leaseCase.AssessmentHistory.First().MatchedPolicyVersion);

        var assessmentTime2 = _actionTime.AddMinutes(2);
        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV2, assessmentTime2);
        leaseCase.RecordAssessmentResult(result2);

        Assert.Equal(2, leaseCase.AssessmentHistory.Count);

        var historyList = leaseCase.AssessmentHistory.ToList();
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, historyList[0].Outcome);
        Assert.Equal("1.0", historyList[0].MatchedPolicyVersion);

        Assert.Equal(RequirementAssessmentOutcome.Required, historyList[1].Outcome);
        Assert.Equal("2.0", historyList[1].MatchedPolicyVersion);

        var latest = leaseCase.GetLatestAssessment(AssessmentSubject.FormalProposal);
        Assert.NotNull(latest);
        Assert.Equal("2.0", latest.MatchedPolicyVersion);
    }

    // 20. Attempts to mutate historical assessment information.
    [Fact]
    public void AssessmentHistory_AttemptsToMutateHistoricalResults_CannotMutate()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);
        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);
        leaseCase.RecordAssessmentResult(result);

        // Verify AssessmentHistory exposed collection cannot be mutated
        var list = leaseCase.AssessmentHistory as IList<RequirementAssessmentResult>;
        Assert.NotNull(list);
        Assert.Throws<NotSupportedException>(() => list.Add(result));
        Assert.Throws<NotSupportedException>(() => list.Clear());

        // Verify RequirementAssessmentResult has no public property setters (immutability)
        var properties = typeof(RequirementAssessmentResult).GetProperties();
        foreach (var prop in properties)
        {
            Assert.Null(prop.GetSetMethod());
        }
    }

    // 21. Relevant Domain-event emission.
    [Fact]
    public void RecordAssessment_EmitsAppropriateDomainEvents()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);

        // Case 1: First definitive assessment -> RequirementAssessmentCompleted
        var policyV1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);
        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV1, _actionTime.AddMinutes(1));
        leaseCase.RecordAssessmentResult(result1);

        var completedEv = leaseCase.DomainEvents.OfType<RequirementAssessmentCompleted>().Single();
        Assert.Equal(_leaseCaseId, completedEv.LeaseCaseId);
        Assert.Equal(AssessmentSubject.FormalProposal, completedEv.Subject);
        Assert.Equal(RequirementAssessmentOutcome.Required, completedEv.Outcome);
        Assert.Equal("1.0", completedEv.PolicyVersion);
        Assert.False(completedEv.RequiresHumanConfirmation);

        // Case 2: Reassessment under new version -> RequirementReassessed
        var policyV2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Policy v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 70.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV2, _actionTime.AddMinutes(2));
        leaseCase.RecordAssessmentResult(result2);

        var reassessedEv = leaseCase.DomainEvents.OfType<RequirementReassessed>().Single();
        Assert.Equal(_leaseCaseId, reassessedEv.LeaseCaseId);
        Assert.Equal(AssessmentSubject.FormalProposal, reassessedEv.Subject);
        Assert.Equal(RequirementAssessmentOutcome.NotRequired, reassessedEv.NewOutcome);
        Assert.Equal("1.0", reassessedEv.PreviousPolicyVersion);
        Assert.Equal("2.0", reassessedEv.NewPolicyVersion);

        // Case 3: Assessment requiring human review -> RequirementAssessmentHumanReviewRequired
        var draftPolicy = new AssessmentPolicy(
            FictionalCabinetPolicyId,
            "1.0",
            "Draft Policy",
            FictionalSourceRef,
            AssessmentSubject.HigherAuthorityPrerequisite,
            PolicyStatus.Draft);

        var result3 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.HigherAuthorityPrerequisite, intake, draftPolicy, _actionTime.AddMinutes(3));
        leaseCase.RecordAssessmentResult(result3);

        var reviewEv = leaseCase.DomainEvents.OfType<RequirementAssessmentHumanReviewRequired>().Single();
        Assert.Equal(_leaseCaseId, reviewEv.LeaseCaseId);
        Assert.Equal(AssessmentSubject.HigherAuthorityPrerequisite, reviewEv.Subject);
        Assert.Contains("Draft status", reviewEv.Reason);
    }

    // 22. No Domain event when construction or assessment fails.
    [Fact]
    public void ConstructionOrAssessmentFailure_EmitsNoDomainEvents()
    {
        // Failure to construct LandExtent
        Assert.Throws<InvalidLandExtentException>(() => new LandExtent(-10m, MeasurementUnit.Acre));

        // Failure to construct MeasurementUnit
        Assert.Throws<InvalidMeasurementUnitException>(() => new MeasurementUnit("InvalidCustomUnit"));

        // Failure to construct AssessmentPolicy
        Assert.Throws<InvalidAssessmentPolicyException>(() => new AssessmentPolicy(
            FictionalProposalPolicyId,
            "",
            "Name",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active));

        // LeaseCase record assessment with mismatched LeaseCaseId throws and does not emit event
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var initialEventCount = leaseCase.DomainEvents.Count;

        var mismatchedResult = new RequirementAssessmentResult(
            new LeaseCaseId(Guid.NewGuid()),
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            FictionalProposalPolicyId.Value,
            FictionalPolicyVersion,
            null,
            _actionTime,
            "Explanation",
            FictionalSourceRef,
            false);

        Assert.Throws<InvalidRequirementAssessmentException>(() => leaseCase.RecordAssessmentResult(mismatchedResult));
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
    }

    // Additional catalogue tests
    [Fact]
    public void PolicyCatalogue_DuplicatePolicy_ThrowsInvalidAssessmentPolicyException()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        catalogue.RegisterPolicy(policy);
        Assert.Throws<InvalidAssessmentPolicyException>(() => catalogue.RegisterPolicy(policy));
    }

    [Fact]
    public void PolicyCatalogue_FindApplicablePolicy_PrefersSpecificOverGeneric()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var genericPolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-GENERIC"),
            "1.0",
            "Generic Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        var commercialPolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-COMMERCIAL"),
            "1.0",
            "Commercial Specific Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial);

        catalogue.RegisterPolicy(genericPolicy);
        catalogue.RegisterPolicy(commercialPolicy);

        var matched = catalogue.FindApplicablePolicy(
            AssessmentSubject.FormalProposal,
            LeasePurpose.Commercial,
            null,
            _actionTime);

        Assert.NotNull(matched);
        Assert.Equal("POL-COMMERCIAL", matched.PolicyId.Value);

        var fallbackMatched = catalogue.FindApplicablePolicy(
            AssessmentSubject.FormalProposal,
            LeasePurpose.Agricultural,
            null,
            _actionTime);

        Assert.NotNull(fallbackMatched);
        Assert.Equal("POL-GENERIC", fallbackMatched.PolicyId.Value);
    }

    // A. Assessment ownership: LeaseCase.RecordAssessmentResult rejects mismatched LeaseCaseId
    [Fact]
    public void RecordAssessmentResult_BelongingToDifferentLeaseCase_ThrowsInvalidRequirementAssessmentException()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var differentLeaseCaseId = new LeaseCaseId(Guid.NewGuid());

        var result = new RequirementAssessmentResult(
            differentLeaseCaseId,
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            FictionalProposalPolicyId.Value,
            FictionalPolicyVersion,
            null,
            _actionTime,
            "Valid explanation",
            FictionalSourceRef,
            false);

        var ex = Assert.Throws<InvalidRequirementAssessmentException>(() => leaseCase.RecordAssessmentResult(result));
        Assert.Contains("does not match this lease case", ex.Message);
    }

    // B. Policy-subject matching: Policy for FormalProposal cannot assess HigherAuthorityPrerequisite, and vice versa
    [Fact]
    public void Assess_PolicySubjectMismatch_ReturnsUndeterminedAndRequiresHumanReview()
    {
        var proposalPolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Proposal Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        var cabinetPolicy = new AssessmentPolicy(
            FictionalCabinetPolicyId,
            FictionalPolicyVersion,
            "Cabinet Policy",
            FictionalSourceRef,
            AssessmentSubject.HigherAuthorityPrerequisite,
            PolicyStatus.Active);

        var intake = CreateValidFictionalIntake();

        // Attempt to assess HigherAuthorityPrerequisite using FormalProposal policy
        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.HigherAuthorityPrerequisite, intake, proposalPolicy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result1.Outcome);
        Assert.True(result1.RequiresHumanConfirmation);
        Assert.Contains("does not match assessed requirement subject", result1.Explanation);

        // Attempt to assess FormalProposal using HigherAuthorityPrerequisite policy
        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, cabinetPolicy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result2.Outcome);
        Assert.True(result2.RequiresHumanConfirmation);
        Assert.Contains("does not match assessed requirement subject", result2.Explanation);
    }

    // C. Ambiguous policy matches: Deterministic evaluation regardless of insertion order
    [Fact]
    public void Assess_AmbiguousPolicies_InsertedInOppositeOrders_ProducesDeterministicHumanReviewResult()
    {
        var policyA = new AssessmentPolicy(
            new AssessmentPolicyId("POL-AMBIGUOUS-A"),
            "1.0",
            "Ambiguous Policy A",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var policyB = new AssessmentPolicy(
            new AssessmentPolicyId("POL-AMBIGUOUS-B"),
            "1.0",
            "Ambiguous Policy B",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 60.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        // Catalogue 1: Insert A, then B
        var cat1 = new AssessmentPolicyCatalogue();
        cat1.RegisterPolicy(policyA);
        cat1.RegisterPolicy(policyB);

        // Catalogue 2: Insert B, then A
        var cat2 = new AssessmentPolicyCatalogue();
        cat2.RegisterPolicy(policyB);
        cat2.RegisterPolicy(policyA);

        var intake = CreateValidFictionalIntake(extentValue: 70.0m, purpose: LeasePurpose.Commercial);

        var result1 = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, cat1, _actionTime);
        var result2 = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, cat2, _actionTime);

        // Both must report AmbiguousMatch / HumanReviewRequired
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result1.Outcome);
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result2.Outcome);
        Assert.True(result1.RequiresHumanConfirmation);
        Assert.True(result2.RequiresHumanConfirmation);

        // Explanations must be identical and deterministic regardless of registration order
        Assert.Equal(result1.Explanation, result2.Explanation);
        Assert.Contains("Multiple active policies match", result1.Explanation);
    }

    // D. Policy identity and version uniqueness
    [Fact]
    public void RegisterPolicy_DifferentVersionsOfSamePolicyId_CoexistSuccessfully()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var v1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        var v2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Policy v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        catalogue.RegisterPolicy(v1);
        catalogue.RegisterPolicy(v2);

        Assert.Equal(2, catalogue.Policies.Count);
        Assert.NotNull(catalogue.GetPolicy(FictionalProposalPolicyId, "1.0"));
        Assert.NotNull(catalogue.GetPolicy(FictionalProposalPolicyId, "2.0"));
    }

    // E. Effective-period evaluation: Start and end boundaries
    [Fact]
    public void EffectivePeriod_EvaluationAtExactBoundaries_EvaluatesAsEffective()
    {
        var start = _actionTime.AddDays(-10);
        var end = _actionTime.AddDays(10);
        var period = new EffectivePeriod(start, end);

        // Exactly at start -> effective
        Assert.True(period.IsEffectiveAt(start));

        // Exactly at end -> effective
        Assert.True(period.IsEffectiveAt(end));

        // 1 second before start -> not effective
        Assert.False(period.IsEffectiveAt(start.AddSeconds(-1)));

        // 1 second after end -> not effective
        Assert.False(period.IsEffectiveAt(end.AddSeconds(1)));
    }

    // F. Unconfirmed comparison logic: below, equal, and above threshold
    [Fact]
    public void Assess_UnconfirmedOperator_BelowEqualAndAboveThreshold_ReturnsHumanReviewRequired()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Unconfirmed Operator Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.Unconfirmed);

        // Below threshold
        var below = CreateValidFictionalIntake(extentValue: 40.0m, unit: MeasurementUnit.Acre);
        var belowResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, below, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, belowResult.Outcome);
        Assert.True(belowResult.RequiresHumanConfirmation);
        Assert.Contains("Comparison operator has not been confirmed", belowResult.Explanation);

        // At threshold
        var equal = CreateValidFictionalIntake(extentValue: 50.0m, unit: MeasurementUnit.Acre);
        var equalResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, equal, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, equalResult.Outcome);
        Assert.True(equalResult.RequiresHumanConfirmation);
        Assert.Contains("Comparison operator has not been confirmed", equalResult.Explanation);

        // Above threshold
        var above = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);
        var aboveResult = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, above, policy, _actionTime);
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, aboveResult.Outcome);
        Assert.True(aboveResult.RequiresHumanConfirmation);
        Assert.Contains("Comparison operator has not been confirmed", aboveResult.Explanation);
    }

    // H & K. Historical immutability & intake updates: older assessment retains its original snapshot
    [Fact]
    public void RecordProposalIntake_SubsequentUpdate_DoesNotMutateEarlierAssessmentSnapshotOrHistory()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);

        var initialIntake = new LeaseProposalIntake(
            LeasePurpose.Commercial,
            new LandExtent(50.0m, MeasurementUnit.Acre),
            new JurisdictionContext("JURIS-1"),
            new IntakeSourceReference("INTAKE-SRC", "1.0"));

        leaseCase.RecordProposalIntake(initialIntake, _actorId, _actionTime, _authority);

        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 75.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, leaseCase.ProposalIntake, policy, _actionTime.AddMinutes(1));
        leaseCase.RecordAssessmentResult(result1);

        // Verify initial assessment snapshot has 50 Acres
        Assert.Single(leaseCase.AssessmentHistory);
        Assert.Equal(50.0m, leaseCase.AssessmentHistory.First().EvaluatedInputs!.ExtentValue);
        Assert.Equal("1.0", leaseCase.AssessmentHistory.First().EvaluatedInputs!.IntakeSourceReference?.Split('v')[1].TrimEnd(')'));

        // Update proposal intake to 150 Acres
        var updatedIntake = new LeaseProposalIntake(
            LeasePurpose.Commercial,
            new LandExtent(150.0m, MeasurementUnit.Acre),
            new JurisdictionContext("JURIS-1"),
            new IntakeSourceReference("INTAKE-SRC", "2.0"));

        leaseCase.RecordProposalIntake(updatedIntake, _actorId, _actionTime.AddMinutes(5), _authority);

        // Historical assessment result #1 must STILL have its original 50.0m value!
        Assert.Equal(50.0m, leaseCase.AssessmentHistory.First().EvaluatedInputs!.ExtentValue);

        // Reassess under updated intake
        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, leaseCase.ProposalIntake, policy, _actionTime.AddMinutes(6));
        leaseCase.RecordAssessmentResult(result2);

        Assert.Equal(2, leaseCase.AssessmentHistory.Count);
        Assert.Equal(50.0m, leaseCase.AssessmentHistory.First().EvaluatedInputs!.ExtentValue);
        Assert.Equal(150.0m, leaseCase.AssessmentHistory.Last().EvaluatedInputs!.ExtentValue);
    }

    // I. Duplicate assessment recording rejection
    [Fact]
    public void RecordAssessmentResult_DuplicateResultId_ThrowsInvalidRequirementAssessmentException()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var resultId = AssessmentResultId.New();

        var result = new RequirementAssessmentResult(
            resultId,
            _leaseCaseId,
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            FictionalProposalPolicyId.Value,
            FictionalPolicyVersion,
            null,
            _actionTime,
            "Valid result",
            FictionalSourceRef,
            false);

        leaseCase.RecordAssessmentResult(result);

        // Recording the exact same result instance or same ID again must throw
        var ex = Assert.Throws<InvalidRequirementAssessmentException>(() => leaseCase.RecordAssessmentResult(result));
        Assert.Contains("already been recorded", ex.Message);
    }

    // J. Domain event semantics: exactly one event emitted per recording operation
    [Fact]
    public void RecordAssessment_EmitsOnlyOneDomainEventPerOperation()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var initialCount = leaseCase.DomainEvents.Count; // 1: LeaseCaseInitialized

        var policyV1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);
        var result1 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV1, _actionTime.AddMinutes(1));
        leaseCase.RecordAssessmentResult(result1);

        // Operation 1 emitted exactly 1 event: RequirementAssessmentCompleted
        Assert.Equal(initialCount + 1, leaseCase.DomainEvents.Count);
        Assert.IsType<RequirementAssessmentCompleted>(leaseCase.DomainEvents.Last());

        // Operation 2: Reassessment
        var policyV2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Policy v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 70.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var result2 = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policyV2, _actionTime.AddMinutes(2));
        leaseCase.RecordAssessmentResult(result2);

        // Operation 2 emitted exactly 1 event: RequirementReassessed
        Assert.Equal(initialCount + 2, leaseCase.DomainEvents.Count);
        Assert.IsType<RequirementReassessed>(leaseCase.DomainEvents.Last());
    }

    // L. Measurement units validation
    [Theory]
    [InlineData("Miles")]
    [InlineData("Kilograms")]
    [InlineData("Liters")]
    [InlineData("UnknownUnit")]
    public void MeasurementUnit_UnsupportedTextualValues_ThrowsInvalidMeasurementUnitException(string invalidUnit)
    {
        var ex = Assert.Throws<InvalidMeasurementUnitException>(() => new MeasurementUnit(invalidUnit));
        Assert.Contains("not a recognised or approved unit", ex.Message);
    }

    [Fact]
    public void MeasurementUnit_UnitIdentity_IsDeterministic()
    {
        var unit1 = new MeasurementUnit("Acre");
        var unit2 = new MeasurementUnit("acres");
        var unit3 = MeasurementUnit.Acre;

        Assert.Equal(unit1, unit2);
        Assert.Equal(unit1, unit3);
        Assert.True(unit1 == unit2);
        Assert.True(unit1.IsCompatibleWith(unit2));
    }

    // D. Policy identity and version uniqueness: Reject duplicate policy and version
    [Fact]
    public void RegisterPolicy_DuplicatePolicyIdAndVersion_ThrowsInvalidAssessmentPolicyException()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var policy1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        var policy2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Duplicate Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active);

        catalogue.RegisterPolicy(policy1);
        var ex = Assert.Throws<InvalidAssessmentPolicyException>(() => catalogue.RegisterPolicy(policy2));
        Assert.Contains("already exists in catalogue", ex.Message);
    }

    // H. Historical immutability: AssessmentHistory cannot be mutated from outside
    [Fact]
    public void AssessmentHistory_AttemptToMutateCollection_ThrowsNotSupportedException()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var result = new RequirementAssessmentResult(
            _leaseCaseId,
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            FictionalProposalPolicyId.Value,
            FictionalPolicyVersion,
            null,
            _actionTime,
            "Valid result",
            FictionalSourceRef,
            false);

        leaseCase.RecordAssessmentResult(result);

        var history = leaseCase.AssessmentHistory;
        var collection = (ICollection<RequirementAssessmentResult>)history;
        Assert.Throws<NotSupportedException>(() => collection.Add(result));
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    // J. Domain events: Rejected recording emits no domain events
    [Fact]
    public void RecordAssessmentResult_WhenValidationFails_EmitsNoDomainEvents()
    {
        var leaseCase = new LeaseCase(_leaseCaseId, "APP-REF-100", _actorId, _actionTime, _authority);
        var initialEventCount = leaseCase.DomainEvents.Count;

        // Mismatched LeaseCaseId
        var mismatchedResult = new RequirementAssessmentResult(
            new LeaseCaseId(Guid.NewGuid()),
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            FictionalProposalPolicyId.Value,
            FictionalPolicyVersion,
            null,
            _actionTime,
            "Valid result",
            FictionalSourceRef,
            false);

        Assert.Throws<InvalidRequirementAssessmentException>(() => leaseCase.RecordAssessmentResult(mismatchedResult));
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);

        // Null result
        Assert.Throws<InvalidRequirementAssessmentException>(() => leaseCase.RecordAssessmentResult(null!));
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
    }

    // G. Inactive policy evaluation
    [Fact]
    public void Assess_InactivePolicy_ReturnsUndeterminedAndRequiresHumanReview()
    {
        var inactivePolicy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Inactive Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Inactive);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, inactivePolicy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("inactive and cannot produce an authoritative determination", result.Explanation);
    }

    // G & C. Catalogue status handling tests
    [Fact]
    public void Catalogue_MatchingDraftPolicy_ProducesHumanReviewRequired()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var draftPolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-DRAFT"),
            "1.0",
            "Draft Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Draft);

        catalogue.RegisterPolicy(draftPolicy);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("Draft status", result.Explanation);
    }

    [Fact]
    public void Catalogue_MatchingAwaitingConfirmationPolicy_ProducesHumanReviewRequired()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var awaitingPolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-AWAITING"),
            "1.0",
            "Awaiting Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.AwaitingConfirmation);

        catalogue.RegisterPolicy(awaitingPolicy);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("awaiting confirmation", result.Explanation);
    }

    [Fact]
    public void Catalogue_MatchingInactivePolicy_ProducesUndetermined()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var inactivePolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-INACTIVE"),
            "1.0",
            "Inactive Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Inactive);

        catalogue.RegisterPolicy(inactivePolicy);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("inactive", result.Explanation);
    }

    [Fact]
    public void Catalogue_MatchingOutOfPeriodPolicy_ProducesUndetermined()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var outOfPeriod = new AssessmentPolicy(
            new AssessmentPolicyId("POL-EXPIRED"),
            "1.0",
            "Expired Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: new EffectivePeriod(_actionTime.AddDays(-30), _actionTime.AddDays(-10)));

        catalogue.RegisterPolicy(outOfPeriod);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("not effective", result.Explanation);
    }

    [Fact]
    public void Catalogue_ActivePolicy_DoesNotBecomeAmbiguousDueToDraftOrInactiveVersion()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var activeV1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Active v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var draftV2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Draft v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Draft,
            extentThreshold: 40.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var inactiveV0 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "0.9",
            "Inactive v0.9",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Inactive);

        catalogue.RegisterPolicy(activeV1);
        catalogue.RegisterPolicy(draftV2);
        catalogue.RegisterPolicy(inactiveV0);

        var intake = CreateValidFictionalIntake(extentValue: 60.0m, unit: MeasurementUnit.Acre);
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Required, result.Outcome);
        Assert.Equal("1.0", result.MatchedPolicyVersion);
        Assert.False(result.RequiresHumanConfirmation);
    }

    // Section 1: Applicability Tests
    [Fact]
    public void Catalogue_MismatchedSpecificPolicy_DoesNotPreventSelectionOfMatchingGenericPolicy()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var commercialSpecific = new AssessmentPolicy(
            new AssessmentPolicyId("POL-COMMERCIAL"),
            "1.0",
            "Commercial Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var genericPolicy = new AssessmentPolicy(
            new AssessmentPolicyId("POL-GENERIC"),
            "1.0",
            "Generic Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            extentThreshold: 100.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        catalogue.RegisterPolicy(commercialSpecific);
        catalogue.RegisterPolicy(genericPolicy);

        // Agricultural intake mismatches commercial policy, so catalogue must select generic policy
        var agriIntake = CreateValidFictionalIntake(extentValue: 120.0m, purpose: LeasePurpose.Agricultural);
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, agriIntake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Required, result.Outcome);
        Assert.Equal("POL-GENERIC", result.MatchedPolicyId);
        Assert.False(result.RequiresHumanConfirmation);
    }

    [Fact]
    public void Catalogue_NoMatchingSpecificOrGenericPolicy_ProducesUndetermined()
    {
        var catalogue = new AssessmentPolicyCatalogue();
        var commercialSpecific = new AssessmentPolicy(
            new AssessmentPolicyId("POL-COMMERCIAL"),
            "1.0",
            "Commercial Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial);

        catalogue.RegisterPolicy(commercialSpecific);

        // Agricultural intake matches neither specific nor generic (none exists)
        var agriIntake = CreateValidFictionalIntake(purpose: LeasePurpose.Agricultural);
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, agriIntake, catalogue, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.Undetermined, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("No applicable", result.Explanation);
    }

    [Fact]
    public void Assess_ConfirmedActiveApplicablePolicy_ProducesNotRequired_WhenThresholdNotExceeded()
    {
        var policy = new AssessmentPolicy(
            FictionalProposalPolicyId,
            FictionalPolicyVersion,
            "Applicable Confirmed Policy",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            applicablePurpose: LeasePurpose.Commercial,
            extentThreshold: 50.0m,
            thresholdUnit: MeasurementUnit.Acre,
            confirmedOperator: ComparisonOperator.GreaterThan);

        var intake = CreateValidFictionalIntake(extentValue: 30.0m, purpose: LeasePurpose.Commercial, unit: MeasurementUnit.Acre);
        var result = RequirementAssessmentEngine.Evaluate(_leaseCaseId, AssessmentSubject.FormalProposal, intake, policy, _actionTime);

        Assert.Equal(RequirementAssessmentOutcome.NotRequired, result.Outcome);
        Assert.False(result.RequiresHumanConfirmation);
        Assert.Contains("does not exceed threshold", result.Explanation);
    }

    // Section 3: Policy Version Tests
    [Fact]
    public void Catalogue_NonOverlappingEffectivePeriods_ResolvesAccordingToAssessmentTime()
    {
        var catalogue = new AssessmentPolicyCatalogue();

        var v1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2025.1",
            "Policy 2025",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: new EffectivePeriod(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc)));

        var v2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2026.1",
            "Policy 2026",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: new EffectivePeriod(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc)));

        catalogue.RegisterPolicy(v1);
        catalogue.RegisterPolicy(v2);

        var intake = CreateValidFictionalIntake();

        // Evaluation in 2025 -> resolves v1
        var eval2025 = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("2025.1", eval2025.MatchedPolicyVersion);
        Assert.Equal(FictionalProposalPolicyId.Value, eval2025.MatchedPolicyId);

        // Evaluation in 2026 -> resolves v2
        var eval2026 = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("2026.1", eval2026.MatchedPolicyVersion);
        Assert.Equal(FictionalProposalPolicyId.Value, eval2026.MatchedPolicyId);
    }

    [Fact]
    public void Catalogue_OverlappingActiveEffectiveVersions_ReturnsHumanReviewRequired()
    {
        var catalogue = new AssessmentPolicyCatalogue();

        var v1 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "1.0",
            "Policy v1",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: new EffectivePeriod(_actionTime.AddMonths(-6), _actionTime.AddMonths(6)));

        var v2 = new AssessmentPolicy(
            FictionalProposalPolicyId,
            "2.0",
            "Policy v2",
            FictionalSourceRef,
            AssessmentSubject.FormalProposal,
            PolicyStatus.Active,
            effectivePeriod: new EffectivePeriod(_actionTime.AddMonths(-1), _actionTime.AddMonths(12)));

        catalogue.RegisterPolicy(v1);
        catalogue.RegisterPolicy(v2);

        var intake = CreateValidFictionalIntake();
        var result = RequirementAssessmentEngine.EvaluateAgainstCatalogue(_leaseCaseId, AssessmentSubject.FormalProposal, intake, catalogue, _actionTime);

        // Must NOT silently select v2 because "2.0" sorts higher
        Assert.Equal(RequirementAssessmentOutcome.HumanReviewRequired, result.Outcome);
        Assert.True(result.RequiresHumanConfirmation);
        Assert.Contains("Multiple active", result.Explanation);
        Assert.Contains("v1.0", result.Explanation);
        Assert.Contains("v2.0", result.Explanation);
    }
}
