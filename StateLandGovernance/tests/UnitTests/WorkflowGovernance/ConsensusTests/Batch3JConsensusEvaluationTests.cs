using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.ConsensusTests;

public class Batch3JConsensusEvaluationTests
{
    private readonly LeaseCaseId _leaseCaseId = new LeaseCaseId(Guid.NewGuid());
    private readonly WorkflowPlanId _planId = new WorkflowPlanId(Guid.NewGuid());
    private readonly WorkflowRuleSetReference _ruleSet = new WorkflowRuleSetReference("RS", "1");
    private readonly DateTime _startedAt = DateTime.UtcNow.AddDays(-10);
    private readonly Guid _officerId = Guid.NewGuid();

    private WorkflowExecution CreateExecution(out WorkflowStageId s1, out WorkflowStageId s2, out WorkflowStageId finalS)
    {
        s1 = new WorkflowStageId(Guid.NewGuid());
        s2 = new WorkflowStageId(Guid.NewGuid());
        finalS = new WorkflowStageId(Guid.NewGuid());
        var code = new WorkflowStageCode("S");
        var inst = new InstitutionCode("INST");
        var d1 = new WorkflowStageDefinition(s1, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var d2 = new WorkflowStageDefinition(s2, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var df = new WorkflowStageDefinition(finalS, code, inst, WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { s1, s2 });
        var def = new WorkflowExecutionDefinition(_planId, 1, _leaseCaseId, _ruleSet, _startedAt.AddDays(-1), _startedAt, new[] { d1, d2, df });
        return new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, _startedAt);
    }

    private void CompleteStage(WorkflowExecution exec, WorkflowStageId sId, WorkflowStageDecisionOutcome outcome, string? reason = "R", IEnumerable<string>? conditions = null)
    {
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        var dt = _startedAt.AddDays(1);
        exec.StartStage(sId, _officerId, dt, auth);
        IReadOnlyCollection<string>? readonlyConditions = conditions?.ToList().AsReadOnly();
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), sId, outcome, reason, readonlyConditions, _officerId, dt.AddHours(1), auth);
    }

    [Fact]
    public void Evaluate_ApprovalRecommended_WhenAllApprove()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(WorkflowExecutionStatus.ReadyForFinalDecision, exec.Status);
        Assert.Equal(ConsensusOutcome.ApprovalRecommended, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_ConditionalApproval_WhenConditionsPresent()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.ApprovedWithConditions, "R", new[] { "C1" });

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.ConditionalApprovalRecommended, exec.ConsensusAssessment!.Outcome);
        Assert.Equal(1, exec.ConsensusAssessment.ConditionalApprovalCount);
    }

    [Fact]
    public void Evaluate_BlockingRejection_TriggersRejectionRecommended()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Rejected, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.RejectionRecommended, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_ChangesRequested_TriggersCorrectionsRequired()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.ChangesRequested, "R", new[] { "C1" });
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.CorrectionsRequired, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_MandatoryAbstention_TriggersHumanEscalation()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Abstained, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.HumanEscalationRequired, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_UnanimousFails_OnSingleAbstention()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Abstained, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), false, false),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), false, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.ConsensusNotReached, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_ThresholdReached_Succeeds()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Abstained, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.ApprovalThreshold, 1, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), false, false),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), false, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.ApprovalRecommended, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_ThresholdNotReached_YieldsConsensusNotReached()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Abstained, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Abstained, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.ApprovalThreshold, 1, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), false, false),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), false, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.ConsensusNotReached, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_AllRejected_YieldsRejectionRecommended()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Rejected, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Rejected, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, false),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        Assert.Equal(ConsensusOutcome.RejectionRecommended, exec.ConsensusAssessment!.Outcome);
    }

    [Fact]
    public void Evaluate_ExactDecisionCounts_AreAccurate()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.ApprovedWithConditions, "R", new[] { "C" });
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Rejected, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, false),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, false)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        
        Assert.Equal(2, exec.ConsensusAssessment!.ParticipantCount);
        Assert.Equal(1, exec.ConsensusAssessment.ApprovalCount);
        Assert.Equal(1, exec.ConsensusAssessment.ConditionalApprovalCount);
        Assert.Equal(1, exec.ConsensusAssessment.RejectionCount);
        Assert.Equal(0, exec.ConsensusAssessment.ChangesRequestedCount);
        Assert.Equal(0, exec.ConsensusAssessment.AbstentionCount);
    }

    [Fact]
    public void Evaluate_WrongExecutionStatus_Throws()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        
        Assert.Throws<InvalidWorkflowExecutionException>(() => exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2)));
    }

    [Fact]
    public void Evaluate_DuplicateAssessment_ThrowsDuplicateException()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved, "R", null);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });

        var id = new ConsensusAssessmentId(Guid.NewGuid());
        var dt = _startedAt.AddDays(2);
        exec.EvaluateConsensus(id, p, dt);

        Assert.Throws<DuplicateConsensusAssessmentException>(() => exec.EvaluateConsensus(id, p, dt));
    }
    
    [Fact]
    public void Evaluate_EventContract_HasExactProperties()
    {
        var exec = CreateExecution(out var s1, out var s2, out var finalS);
        CompleteStage(exec, s1, WorkflowStageDecisionOutcome.Approved, "R", null);
        CompleteStage(exec, s2, WorkflowStageDecisionOutcome.Approved, "R", null);
        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true),
            new ConsensusParticipantRule(s2, new InstitutionCode("INST"), true, true)
        });
        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        var e = exec.DomainEvents.OfType<ConsensusAssessmentRecorded>().Last();
        Assert.Equal(exec.Id, e.WorkflowExecutionId);
        Assert.Equal(exec.ConsensusAssessment!.AssessmentId, e.ConsensusAssessmentId);
    }
}



