using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Collections.Generic;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.ConsensusTests;

public class Batch3JConsensusPolicyTests
{
    private readonly ConsensusPolicyId _policyId = new ConsensusPolicyId(Guid.NewGuid());
    private readonly LeaseCaseId _leaseCaseId = new LeaseCaseId(Guid.NewGuid());
    private readonly WorkflowPlanId _planId = new WorkflowPlanId(Guid.NewGuid());
    private readonly WorkflowRuleSetReference _ruleSet = new WorkflowRuleSetReference("RS", "1");
    private readonly DateTime _generatedAt = DateTime.UtcNow;

    private ConsensusParticipantRule CreateParticipant(string code, bool mandatory, bool blocking) =>
        new ConsensusParticipantRule(new WorkflowStageId(Guid.NewGuid()), new InstitutionCode(code), mandatory, blocking);

    [Fact]
    public void Policy_ValidUnanimous_IsCreated()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        var policy = new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, participants);
        Assert.NotNull(policy);
        Assert.Equal(ConsensusRuleType.Unanimous, policy.RuleType);
    }

    [Fact]
    public void Policy_ValidThreshold_IsCreated()
    {
        var participants = new[] { CreateParticipant("A", true, true), CreateParticipant("B", true, true) };
        var policy = new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.ApprovalThreshold, 1, _generatedAt, participants);
        Assert.NotNull(policy);
        Assert.Equal(ConsensusRuleType.ApprovalThreshold, policy.RuleType);
    }

    [Fact]
    public void Policy_InvalidId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ConsensusPolicyId(Guid.Empty));
    }

    [Fact]
    public void Policy_MissingIdentifier_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, participants));
    }

    [Fact]
    public void Policy_MissingVersion_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, participants));
    }

    [Fact]
    public void Policy_InvalidTimestamp_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, DateTime.Now, participants));
    }

    [Fact]
    public void Policy_EmptyParticipants_Throws()
    {
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, new ConsensusParticipantRule[0]));
    }

    [Fact]
    public void Policy_DuplicateParticipants_Throws()
    {
        var p1 = CreateParticipant("A", true, true);
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, new[] { p1, p1 }));
    }

    [Fact]
    public void Policy_InvalidThresholdCount_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.ApprovalThreshold, 0, _generatedAt, participants));
    }

    [Fact]
    public void Policy_ThresholdExceedsParticipants_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.ApprovalThreshold, 2, _generatedAt, participants));
    }

    [Fact]
    public void Policy_UnanimousWithThreshold_Throws()
    {
        var participants = new[] { CreateParticipant("A", true, true) };
        Assert.Throws<InvalidConsensusPolicyException>(() => new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, 1, _generatedAt, participants));
    }

    [Fact]
    public void Policy_ParticipantsCollection_IsDefensivelyCopied()
    {
        var p1 = CreateParticipant("A", true, true);
        var list = new List<ConsensusParticipantRule> { p1 };
        var policy = new ConsensusPolicySnapshot(_policyId, "P1", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _generatedAt, list);
        list.Clear();
        Assert.Single(policy.Participants);
    }
}
