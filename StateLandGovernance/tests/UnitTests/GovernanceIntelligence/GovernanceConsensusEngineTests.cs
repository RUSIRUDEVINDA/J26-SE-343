using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceConsensusEngineTests
{
    private readonly GovernanceConsensusEngine _engine;
    private readonly DateTime _testTimestamp;

    public GovernanceConsensusEngineTests()
    {
        _engine = new GovernanceConsensusEngine();
        _testTimestamp = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Evaluate_UnanimousApproval_ShouldReturnConsensusReached()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.Unanimous,
            new[] { "INST-A", "INST-B", "INST-C" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Approve)
        };

        var result = _engine.EvaluateConsensus("PARCEL-101", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusReached, result.Outcome);
        Assert.Equal(3, result.ApprovalCount);
        Assert.True(result.ConsensusThresholdSatisfied);
        Assert.True(result.QuorumSatisfied);
    }

    [Fact]
    public void Evaluate_UnanimousPolicy_SingleRejection_ShouldReturnConsensusNotReached()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.Unanimous,
            new[] { "INST-A", "INST-B", "INST-C", "INST-D", "INST-E" },
            isRejectionBlocking: false);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-D", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-E", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-102", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusNotReached, result.Outcome);
        Assert.False(result.ConsensusThresholdSatisfied);
    }

    [Fact]
    public void Evaluate_MajoritySuccess_DespiteOrdinaryMinorityRejection()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "INST-A", "INST-B", "INST-C", "INST-D" },
            isRejectionBlocking: false);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-D", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-103", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusReached, result.Outcome);
        Assert.Equal(3, result.ApprovalCount);
        Assert.Equal(1, result.RejectionCount);
        Assert.True(result.ConsensusThresholdSatisfied);
    }

    [Fact]
    public void Evaluate_SimpleMajority_BelowThreshold_ShouldReturnConsensusNotReached()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "INST-A", "INST-B", "INST-C", "INST-D" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Reject),
            new InstitutionalGovernancePosition("INST-D", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-104", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusNotReached, result.Outcome);
        Assert.False(result.ConsensusThresholdSatisfied);
    }

    [Fact]
    public void Evaluate_Supermajority_ExactBoundary_ShouldReturnConsensusReached()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.Supermajority,
            new[] { "INST-A", "INST-B", "INST-C" },
            requiredPercentage: 66.0);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-105", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusReached, result.Outcome);
        Assert.True(result.ConsensusThresholdSatisfied);
    }

    [Fact]
    public void Evaluate_Supermajority_BelowBoundary_ShouldReturnConsensusNotReached()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.Supermajority,
            new[] { "INST-A", "INST-B", "INST-C", "INST-D", "INST-E" },
            requiredPercentage: 75.0);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-C", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-D", InstitutionalPositionType.Reject),
            new InstitutionalGovernancePosition("INST-E", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-106", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusNotReached, result.Outcome);
        Assert.False(result.ConsensusThresholdSatisfied);
    }

    [Fact]
    public void Evaluate_Expected5Institutions_Only2Submitted_QuorumDenominator()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "INST-1", "INST-2", "INST-3", "INST-4", "INST-5" },
            minQuorumPercentage: 50.0);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("INST-1", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("INST-2", InstitutionalPositionType.Approve)
        };

        var result = _engine.EvaluateConsensus("PARCEL-107", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.InsufficientQuorum, result.Outcome);
        Assert.Equal(5, result.TotalExpectedInstitutions);
        Assert.Equal(2, result.SubmittedCount);
        Assert.Equal(3, result.MissingCount);
        Assert.False(result.QuorumSatisfied);
    }

    [Fact]
    public void Evaluate_QuorumNotSatisfied_ShouldReturnInsufficientQuorum()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C", "D" },
            minQuorumPercentage: 75.0);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve)
        };

        var result = _engine.EvaluateConsensus("PARCEL-108", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.InsufficientQuorum, result.Outcome);
    }

    [Fact]
    public void Evaluate_MandatoryInstitution_PresentAndApproved_ShouldSucceed()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "CEA", "UDA", "LCC" },
            mandatoryInstitutionIds: new[] { "CEA" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("CEA", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("UDA", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("LCC", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-109", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConsensusReached, result.Outcome);
        Assert.True(result.MandatoryInstitutionsSatisfied);
    }

    [Fact]
    public void Evaluate_MandatoryInstitution_Missing_ShouldReturnPending()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "CEA", "UDA", "LCC" },
            mandatoryInstitutionIds: new[] { "CEA" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("UDA", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("LCC", InstitutionalPositionType.Approve)
        };

        var result = _engine.EvaluateConsensus("PARCEL-110", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.Pending, result.Outcome);
        Assert.False(result.MandatoryInstitutionsSatisfied);
        Assert.Equal(1, result.MissingCount);
    }

    [Fact]
    public void Evaluate_MandatoryInstitution_Rejects_ShouldReturnBlocked()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "CEA", "UDA", "LCC" },
            mandatoryInstitutionIds: new[] { "CEA" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("CEA", InstitutionalPositionType.Reject),
            new InstitutionalGovernancePosition("UDA", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("LCC", InstitutionalPositionType.Approve)
        };

        var result = _engine.EvaluateConsensus("PARCEL-111", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.Blocked, result.Outcome);
        Assert.Equal(1, result.BlockingInstitutionCount);
    }

    [Fact]
    public void Evaluate_MandatoryInstitution_OutsideExpectedSet_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            expectedInstitutionIds: new[] { "UDA", "LCC" },
            mandatoryInstitutionIds: new[] { "CEA" }));
    }

    [Fact]
    public void Evaluate_ThresholdCount_MissingOrInvalidApprovalCount_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GovernanceConsensusPolicy(
            ConsensusType.ThresholdCount,
            expectedInstitutionIds: new[] { "A", "B", "C" },
            requiredApprovalCount: null));

        Assert.Throws<ArgumentOutOfRangeException>(() => new GovernanceConsensusPolicy(
            ConsensusType.ThresholdCount,
            expectedInstitutionIds: new[] { "A", "B", "C" },
            requiredApprovalCount: 0));
    }

    [Fact]
    public void Evaluate_ThresholdCount_ExceedsExpectedCount_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GovernanceConsensusPolicy(
            ConsensusType.ThresholdCount,
            expectedInstitutionIds: new[] { "A", "B", "C" },
            requiredApprovalCount: 5));
    }

    [Fact]
    public void Evaluate_Supermajority_InvalidPercentage_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GovernanceConsensusPolicy(
            ConsensusType.Supermajority,
            expectedInstitutionIds: new[] { "A", "B", "C" },
            requiredPercentage: 45.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => new GovernanceConsensusPolicy(
            ConsensusType.Supermajority,
            expectedInstitutionIds: new[] { "A", "B", "C" },
            requiredPercentage: 105.0));
    }

    [Fact]
    public void Evaluate_ConditionalApproval_DisallowedByPolicy_GeneratesConditionalConsensus()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C" },
            allowConditionalAsApproval: false);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("C", InstitutionalPositionType.ConditionalApprove)
        };

        var result = _engine.EvaluateConsensus("PARCEL-112", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.ConditionalConsensus, result.Outcome);
        Assert.Equal(1, result.ConditionalApprovalCount);
    }

    [Fact]
    public void Evaluate_Abstention_TreatmentInQuorum()
    {
        var policyWithAbstention = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C", "D" },
            minQuorumPercentage: 50.0,
            countAbstentionsInQuorum: true);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Abstain)
        };

        var result = _engine.EvaluateConsensus("PARCEL-113", positions, policyWithAbstention, _testTimestamp);

        Assert.True(result.QuorumSatisfied);
        Assert.Equal(2, result.ParticipatingCount);
        Assert.Equal(1, result.AbstentionCount);
    }

    [Fact]
    public void Evaluate_MissingVsPending_Distinction()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C", "D" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Pending)
        };

        var result = _engine.EvaluateConsensus("PARCEL-114", positions, policy, _testTimestamp);

        Assert.Equal(2, result.SubmittedCount);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(2, result.MissingCount);
        Assert.Equal(4, result.TotalExpectedInstitutions);
    }

    [Fact]
    public void Evaluate_SubmittedVsParticipatingVsMissing_CountAccuracy()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C", "D", "E" },
            countAbstentionsInQuorum: true);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Reject),
            new InstitutionalGovernancePosition("C", InstitutionalPositionType.Abstain),
            new InstitutionalGovernancePosition("D", InstitutionalPositionType.Pending)
        };

        var result = _engine.EvaluateConsensus("PARCEL-115", positions, policy, _testTimestamp);

        Assert.Equal(5, result.TotalExpectedInstitutions);
        Assert.Equal(4, result.SubmittedCount);
        Assert.Equal(3, result.ParticipatingCount);
        Assert.Equal(1, result.MissingCount);
        Assert.Equal(1, result.PendingCount);
    }

    [Fact]
    public void Evaluate_BlockingRejectionPolicy_Enabled()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B", "C" },
            isRejectionBlocking: true);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("C", InstitutionalPositionType.Reject)
        };

        var result = _engine.EvaluateConsensus("PARCEL-116", positions, policy, _testTimestamp);

        Assert.Equal(ConsensusOutcome.Blocked, result.Outcome);
        Assert.Equal(1, result.BlockingInstitutionCount);
    }

    [Fact]
    public void Evaluate_UnexpectedInstitution_ShouldThrowArgumentException()
    {
        var policy = new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            new[] { "A", "B" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("UNEXPECTED_XYZ", InstitutionalPositionType.Approve)
        };

        Assert.Throws<ArgumentException>(() => _engine.EvaluateConsensus("PARCEL-117", positions, policy, _testTimestamp));
    }

    [Fact]
    public void Evaluate_EmptyExpectedInstitutions_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GovernanceConsensusPolicy(
            ConsensusType.SimpleMajority,
            expectedInstitutionIds: Array.Empty<string>()));
    }

    [Fact]
    public void Evaluate_NullInput_ShouldThrowArgumentNullException()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A" });

        Assert.Throws<ArgumentException>(() => _engine.EvaluateConsensus(" ", new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve) }, policy, _testTimestamp));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateConsensus("PARCEL-118", new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve) }, null!, _testTimestamp));
    }

    [Fact]
    public void Evaluate_DuplicateInstitutionIdentifiers_ShouldThrowArgumentException()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B" });

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Reject)
        };

        Assert.Throws<ArgumentException>(() => _engine.EvaluateConsensus("PARCEL-119", positions, policy, _testTimestamp));
    }

    [Fact]
    public void Evaluate_SamePositions_DifferentQuorumThresholds_ProducesDifferentEvaluationIds()
    {
        var policy1 = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B" }, minQuorumPercentage: 50.0);
        var policy2 = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B" }, minQuorumPercentage: 75.0);

        var positions = new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve) };

        var res1 = _engine.EvaluateConsensus("PARCEL-120", positions, policy1, _testTimestamp);
        var res2 = _engine.EvaluateConsensus("PARCEL-120", positions, policy2, _testTimestamp);

        Assert.NotEqual(res1.ConsensusEvaluationId, res2.ConsensusEvaluationId);
    }

    [Fact]
    public void Evaluate_SamePositions_DifferentApprovalThresholds_ProducesDifferentEvaluationIds()
    {
        var policy1 = new GovernanceConsensusPolicy(ConsensusType.Supermajority, new[] { "A", "B", "C" }, requiredPercentage: 66.67);
        var policy2 = new GovernanceConsensusPolicy(ConsensusType.Supermajority, new[] { "A", "B", "C" }, requiredPercentage: 75.0);

        var positions = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve)
        };

        var res1 = _engine.EvaluateConsensus("PARCEL-121", positions, policy1, _testTimestamp);
        var res2 = _engine.EvaluateConsensus("PARCEL-121", positions, policy2, _testTimestamp);

        Assert.NotEqual(res1.ConsensusEvaluationId, res2.ConsensusEvaluationId);
    }

    [Fact]
    public void Evaluate_ReorderedExpectedAndMandatoryIds_ProducesIdenticalResultAndId()
    {
        var policy1 = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B", "C" }, mandatoryInstitutionIds: new[] { "A", "B" });
        var policy2 = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "C", "B", "A" }, mandatoryInstitutionIds: new[] { "B", "A" });

        var positions = new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve), new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve) };

        var res1 = _engine.EvaluateConsensus("PARCEL-122", positions, policy1, _testTimestamp);
        var res2 = _engine.EvaluateConsensus("PARCEL-122", positions, policy2, _testTimestamp);

        Assert.Equal(res1.ConsensusEvaluationId, res2.ConsensusEvaluationId);
        Assert.Equal(res1.Outcome, res2.Outcome);
    }

    [Fact]
    public void Evaluate_ReversedInputOrdering_ShouldProduceIdenticalResult()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B" });

        var posForward = new[]
        {
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve),
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Reject)
        };

        var posReversed = new[]
        {
            new InstitutionalGovernancePosition("B", InstitutionalPositionType.Reject),
            new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve)
        };

        var res1 = _engine.EvaluateConsensus("PARCEL-123", posForward, policy, _testTimestamp);
        var res2 = _engine.EvaluateConsensus("PARCEL-123", posReversed, policy, _testTimestamp);

        Assert.Equal(res1.ConsensusEvaluationId, res2.ConsensusEvaluationId);
        Assert.Equal(res1.Outcome, res2.Outcome);
    }

    [Fact]
    public void Evaluate_IdentifierCasingAndWhitespace_ShouldBeNormalized()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "  inst-a  ", "inst-b" });

        var positions = new[] { new InstitutionalGovernancePosition("inst-a", InstitutionalPositionType.Approve) };

        var res = _engine.EvaluateConsensus("parcel-124 ", positions, policy, _testTimestamp);

        Assert.Equal("PARCEL-124", res.SubjectId);
        Assert.StartsWith("CNS-EVAL-", res.ConsensusEvaluationId);
    }

    [Fact]
    public void Evaluate_OutcomeExplanation_ShouldBeHumanReadable()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A", "B" });
        var positions = new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve), new InstitutionalGovernancePosition("B", InstitutionalPositionType.Approve) };

        var res = _engine.EvaluateConsensus("PARCEL-125", positions, policy, _testTimestamp);

        Assert.NotNull(res.SummaryExplanation);
        Assert.Contains("Consensus successfully reached", res.SummaryExplanation);
        Assert.NotNull(res.RecommendedAction);
    }

    [Fact]
    public void Evaluate_Result_ShouldContainSharedTimestamp()
    {
        var policy = new GovernanceConsensusPolicy(ConsensusType.SimpleMajority, new[] { "A" });
        var positions = new[] { new InstitutionalGovernancePosition("A", InstitutionalPositionType.Approve) };

        var res = _engine.EvaluateConsensus("PARCEL-126", positions, policy, _testTimestamp);

        Assert.Equal(_testTimestamp, res.EvaluationTimestamp);
    }
}
