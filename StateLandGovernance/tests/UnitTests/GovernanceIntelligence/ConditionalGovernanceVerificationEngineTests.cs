using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class ConditionalGovernanceVerificationEngineTests
{
    private readonly ConditionalGovernanceVerificationEngine _engine;
    private readonly DateTime _baseEvaluationTime;

    public ConditionalGovernanceVerificationEngineTests()
    {
        _engine = new ConditionalGovernanceVerificationEngine();
        _baseEvaluationTime = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Evaluate_AllMandatoryConditionsSatisfied_ReturnsFullySatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-ENV", "Environmental Clearance", "Environmental", isMandatory: true),
            new("COND-VAL", "Valuation Report", "Valuation", isMandatory: true)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-ENV", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-VAL", providedStatus: SuppliedEvidenceStatus.Satisfied)
        };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.FullySatisfied, result.Outcome);
        Assert.Equal(2, result.TotalConditionsCount);
        Assert.Equal(2, result.SatisfiedMandatoryCount);
        Assert.Equal(0, result.UnsatisfiedMandatoryCount);
    }

    [Fact]
    public void Evaluate_MandatoryFailed_ReturnsUnsatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-ENV", "Environmental Clearance", "Environmental", isMandatory: true)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-ENV", providedStatus: SuppliedEvidenceStatus.Failed, remarks: "Rejected by Environmental Authority")
        };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.Unsatisfied, result.Outcome);
        Assert.Equal(1, result.UnsatisfiedMandatoryCount);
        Assert.Equal(DerivedConditionStatus.Failed, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_MandatoryExpired_ReturnsUnsatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-ENV", "Environmental Clearance", "Environmental", isMandatory: true)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-ENV", providedStatus: SuppliedEvidenceStatus.Satisfied, expiryTimestamp: _baseEvaluationTime.AddDays(-1))
        };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.Unsatisfied, result.Outcome);
        Assert.Equal(DerivedConditionStatus.Expired, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_MandatoryEvidenceMissing_ReturnsPendingEvidence()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-ENV", "Environmental Clearance", "Environmental", isMandatory: true)
        };

        var evidence = new List<VerificationEvidence>();

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.PendingEvidence, result.Outcome);
        Assert.Equal(1, result.MissingConditionsCount);
        Assert.Equal(DerivedConditionStatus.MissingEvidence, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_MandatoryEvidencePending_ReturnsPendingEvidence()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-ENV", "Environmental Clearance", "Environmental", isMandatory: true)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-ENV", providedStatus: SuppliedEvidenceStatus.Pending)
        };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.PendingEvidence, result.Outcome);
        Assert.Equal(1, result.PendingConditionsCount);
        Assert.Equal(DerivedConditionStatus.Pending, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_OptionalFailed_WithAllowProvisionalTrue_ReturnsProvisionallySatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-MAND", "Mandatory Clearance", "Statutory", isMandatory: true),
            new("COND-OPT", "Optional Clearance", "Discretionary", isMandatory: false)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-MAND", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-OPT", providedStatus: SuppliedEvidenceStatus.Failed)
        };

        var policy = new ConditionalVerificationPolicy(AllowProvisionalVerification: true);
        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, policy, _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.ProvisionallySatisfied, result.Outcome);
        Assert.Equal(1, result.SatisfiedMandatoryCount);
    }

    [Fact]
    public void Evaluate_OptionalMissing_WithAllowProvisionalTrue_ReturnsProvisionallySatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-MAND", "Mandatory Clearance", "Statutory", isMandatory: true),
            new("COND-OPT", "Optional Clearance", "Discretionary", isMandatory: false)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-MAND", providedStatus: SuppliedEvidenceStatus.Satisfied)
        };

        var policy = new ConditionalVerificationPolicy(AllowProvisionalVerification: true);
        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, policy, _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.ProvisionallySatisfied, result.Outcome);
    }

    [Fact]
    public void Evaluate_OptionalFailed_WithAllowProvisionalFalse_ReturnsUnsatisfied()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-MAND", "Mandatory Clearance", "Statutory", isMandatory: true),
            new("COND-OPT", "Optional Clearance", "Discretionary", isMandatory: false)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-MAND", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-OPT", providedStatus: SuppliedEvidenceStatus.Failed)
        };

        var policy = new ConditionalVerificationPolicy(AllowProvisionalVerification: false);
        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, policy, _baseEvaluationTime);

        Assert.Equal(ConditionalVerificationOutcome.Unsatisfied, result.Outcome);
    }

    [Fact]
    public void Evaluate_MixedMandatoryAndOptionalConditions_EvaluatesCorrectCountsAndOutcome()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-1", "Mandatory 1", isMandatory: true),
            new("COND-2", "Mandatory 2", isMandatory: true),
            new("COND-3", "Optional 1", isMandatory: false),
            new("COND-4", "Optional 2", isMandatory: false)
        };

        var evidence = new List<VerificationEvidence>
        {
            new("COND-1", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-2", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-3", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new("COND-4", providedStatus: SuppliedEvidenceStatus.Pending)
        };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(AllowProvisionalVerification: true), _baseEvaluationTime);

        Assert.Equal(4, result.TotalConditionsCount);
        Assert.Equal(2, result.MandatoryConditionsCount);
        Assert.Equal(2, result.SatisfiedMandatoryCount);
        Assert.Equal(1, result.SatisfiedOptionalCount);
        Assert.Equal(1, result.PendingConditionsCount);
        Assert.Equal(ConditionalVerificationOutcome.ProvisionallySatisfied, result.Outcome);
    }

    [Fact]
    public void Evaluate_ExplicitExpiryTimestampBeforeEvaluationTime_DerivesExpiredStatus()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1", isMandatory: true) };
        var evidence = new List<VerificationEvidence> { new("COND-1", providedStatus: SuppliedEvidenceStatus.Satisfied, expiryTimestamp: _baseEvaluationTime.AddMinutes(-5)) };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(DerivedConditionStatus.Expired, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_NonExpiredEvidence_DerivesSatisfiedStatus()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1", isMandatory: true) };
        var evidence = new List<VerificationEvidence> { new("COND-1", providedStatus: SuppliedEvidenceStatus.Satisfied, expiryTimestamp: _baseEvaluationTime.AddDays(10)) };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(DerivedConditionStatus.Satisfied, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_ExpiryEqualityBoundary_DerivesExpiredStatus()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1", isMandatory: true) };
        var evidence = new List<VerificationEvidence> { new("COND-1", providedStatus: SuppliedEvidenceStatus.Satisfied, expiryTimestamp: _baseEvaluationTime) };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(DerivedConditionStatus.Expired, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_MaxEvidenceAgeDaysExceeded_DerivesExpiredStatus()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1", isMandatory: true, maxEvidenceAgeDays: 30) };
        var evidence = new List<VerificationEvidence> { new("COND-1", providedStatus: SuppliedEvidenceStatus.Satisfied, evidenceTimestamp: _baseEvaluationTime.AddDays(-31)) };

        var result = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(DerivedConditionStatus.Expired, result.ConditionStatuses.First().Status);
    }

    [Fact]
    public void Evaluate_EmptyConditionsCollection_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _engine.EvaluateVerification("SUBJ-PARCEL-100", new List<GovernanceCondition>(), new List<VerificationEvidence>(), new ConditionalVerificationPolicy(), _baseEvaluationTime));
    }

    [Fact]
    public void Evaluate_NullConditionsCollection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateVerification("SUBJ-PARCEL-100", null!, new List<VerificationEvidence>(), new ConditionalVerificationPolicy(), _baseEvaluationTime));
    }

    [Fact]
    public void Evaluate_DuplicateConditionIdsInConditionsInput_ThrowsArgumentException()
    {
        var conditions = new List<GovernanceCondition>
        {
            new("COND-1", "Condition 1"),
            new("cond-1", "Condition 1 Duplicate")
        };

        Assert.Throws<ArgumentException>(() => _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, new List<VerificationEvidence>(), new ConditionalVerificationPolicy(), _baseEvaluationTime));
    }

    [Fact]
    public void Evaluate_DuplicateEvidenceForSameCondition_ThrowsArgumentException()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1") };
        var evidence = new List<VerificationEvidence>
        {
            new("COND-1", "EVID-A"),
            new("cond-1", "EVID-B")
        };

        Assert.Throws<ArgumentException>(() => _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime));
    }

    [Fact]
    public void Evaluate_UnexpectedEvidenceConditionId_ThrowsArgumentException()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1") };
        var evidence = new List<VerificationEvidence>
        {
            new("COND-1"),
            new("COND-UNKNOWN")
        };

        var ex = Assert.Throws<ArgumentException>(() => _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime));
        Assert.Contains("Unexpected evidence provided for unknown condition ID", ex.Message);
    }

    [Fact]
    public void Evaluate_DeterministicRepeatedExecution_ReturnsIdenticalResultAndId()
    {
        var conditions = new List<GovernanceCondition> { new("COND-1"), new("COND-2") };
        var evidence = new List<VerificationEvidence> { new("COND-1"), new("COND-2") };

        var res1 = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);
        var res2 = _engine.EvaluateVerification("SUBJ-PARCEL-100", conditions, evidence, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(res1.VerificationId, res2.VerificationId);
        Assert.Equal(res1.Outcome, res2.Outcome);
    }

    [Fact]
    public void Evaluate_ReversedConditionsOrder_ReturnsIdenticalVerificationId()
    {
        var c1 = new GovernanceCondition("COND-A");
        var c2 = new GovernanceCondition("COND-B");
        var ev = new List<VerificationEvidence> { new("COND-A"), new("COND-B") };

        var resForward = _engine.EvaluateVerification("SUBJ-PARCEL-100", new[] { c1, c2 }, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);
        var resReverse = _engine.EvaluateVerification("SUBJ-PARCEL-100", new[] { c2, c1 }, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(resForward.VerificationId, resReverse.VerificationId);
    }

    [Fact]
    public void Evaluate_ReversedEvidenceOrder_ReturnsIdenticalVerificationId()
    {
        var conds = new[] { new GovernanceCondition("COND-A"), new GovernanceCondition("COND-B") };
        var e1 = new VerificationEvidence("COND-A");
        var e2 = new VerificationEvidence("COND-B");

        var resForward = _engine.EvaluateVerification("SUBJ-PARCEL-100", conds, new[] { e1, e2 }, new ConditionalVerificationPolicy(), _baseEvaluationTime);
        var resReverse = _engine.EvaluateVerification("SUBJ-PARCEL-100", conds, new[] { e2, e1 }, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(resForward.VerificationId, resReverse.VerificationId);
    }

    [Fact]
    public void Evaluate_CasingAndWhitespaceNormalization_ReturnsIdenticalVerificationId()
    {
        var c1 = new GovernanceCondition(" COND-A ");
        var c2 = new GovernanceCondition("cond-a");
        var ev = new[] { new VerificationEvidence("cond-a") };

        var res1 = _engine.EvaluateVerification(" SUBJ-PARCEL-100 ", new[] { c1 }, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);
        var res2 = _engine.EvaluateVerification("subj-parcel-100", new[] { c2 }, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(res1.VerificationId, res2.VerificationId);
    }

    [Fact]
    public void Evaluate_OutcomeAffectingPolicyChange_ProducesDifferentVerificationId()
    {
        var conds = new[] { new GovernanceCondition("COND-MAND", isMandatory: true), new GovernanceCondition("COND-OPT", isMandatory: false) };
        var ev = new[] { new VerificationEvidence("COND-MAND") };

        var resProvTrue = _engine.EvaluateVerification("SUBJ-1", conds, ev, new ConditionalVerificationPolicy(AllowProvisionalVerification: true), _baseEvaluationTime);
        var resProvFalse = _engine.EvaluateVerification("SUBJ-1", conds, ev, new ConditionalVerificationPolicy(AllowProvisionalVerification: false), _baseEvaluationTime);

        Assert.NotEqual(resProvTrue.VerificationId, resProvFalse.VerificationId);
    }

    [Fact]
    public void Evaluate_AggregateCountAccuracy_ComputesExactCounts()
    {
        var conds = new[]
        {
            new GovernanceCondition("C1", isMandatory: true),
            new GovernanceCondition("C2", isMandatory: true),
            new GovernanceCondition("C3", isMandatory: true),
            new GovernanceCondition("C4", isMandatory: true),
            new GovernanceCondition("C5", isMandatory: false)
        };

        var ev = new[]
        {
            new VerificationEvidence("C1", providedStatus: SuppliedEvidenceStatus.Satisfied),
            new VerificationEvidence("C2", providedStatus: SuppliedEvidenceStatus.Failed),
            new VerificationEvidence("C3", providedStatus: SuppliedEvidenceStatus.Pending),
            new VerificationEvidence("C4", providedStatus: SuppliedEvidenceStatus.Satisfied, expiryTimestamp: _baseEvaluationTime.AddDays(-1)),
            new VerificationEvidence("C5", providedStatus: SuppliedEvidenceStatus.Satisfied)
        };

        var res = _engine.EvaluateVerification("SUBJ-COUNT", conds, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(5, res.TotalConditionsCount);
        Assert.Equal(4, res.MandatoryConditionsCount);
        Assert.Equal(1, res.SatisfiedMandatoryCount);
        Assert.Equal(3, res.UnsatisfiedMandatoryCount);
        Assert.Equal(1, res.SatisfiedOptionalCount);
        Assert.Equal(1, res.PendingConditionsCount);
        Assert.Equal(0, res.MissingConditionsCount);
        Assert.Equal(1, res.ExpiredConditionsCount);
    }

    [Fact]
    public void Evaluate_NeutralHumanReadableExplanation_GeneratesNonLegalLanguage()
    {
        var conds = new[] { new GovernanceCondition("C1") };
        var ev = new[] { new VerificationEvidence("C1") };

        var res = _engine.EvaluateVerification("SUBJ-1", conds, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.DoesNotContain("legal guilt", res.SummaryExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fraud", res.SummaryExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("corruption", res.SummaryExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_HumanReviewRecommendation_SuggestsNeutralAction()
    {
        var conds = new[] { new GovernanceCondition("C1", isMandatory: true) };
        var res = _engine.EvaluateVerification("SUBJ-1", conds, new List<VerificationEvidence>(), new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Contains("Human review recommended", res.RecommendedAction);
    }

    [Fact]
    public void Evaluate_SharedEvaluationTimestamp_UsesSuppliedTimestamp()
    {
        var conds = new[] { new GovernanceCondition("C1") };
        var ev = new[] { new VerificationEvidence("C1") };

        var res = _engine.EvaluateVerification("SUBJ-1", conds, ev, new ConditionalVerificationPolicy(), _baseEvaluationTime);

        Assert.Equal(_baseEvaluationTime, res.EvaluationTimestamp);
    }
}
