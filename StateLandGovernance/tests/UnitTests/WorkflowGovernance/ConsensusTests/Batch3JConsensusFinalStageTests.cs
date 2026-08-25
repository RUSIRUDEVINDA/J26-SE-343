using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.ConsensusTests;

public class Batch3JConsensusFinalStageTests
{
    private readonly LeaseCaseId _leaseCaseId = new LeaseCaseId(Guid.NewGuid());
    private readonly WorkflowPlanId _planId = new WorkflowPlanId(Guid.NewGuid());
    private readonly WorkflowRuleSetReference _ruleSet = new WorkflowRuleSetReference("RS", "1");
    private readonly DateTime _startedAt = DateTime.UtcNow.AddDays(-10);
    private readonly Guid _officerId = Guid.NewGuid();

    private WorkflowExecution CreateEvaluatedExecution(out WorkflowStageId s1, out WorkflowStageId finalS)
    {
        s1 = new WorkflowStageId(Guid.NewGuid());
        finalS = new WorkflowStageId(Guid.NewGuid());
        var code = new WorkflowStageCode("S");
        var inst = new InstitutionCode("INST");
        var d1 = new WorkflowStageDefinition(s1, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var df = new WorkflowStageDefinition(finalS, code, inst, WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { s1 });
        var def = new WorkflowExecutionDefinition(_planId, 1, _leaseCaseId, _ruleSet, _startedAt.AddDays(-1), _startedAt, new[] { d1, df });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, _startedAt);

        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, inst, new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        var dt = _startedAt.AddDays(1);
        exec.StartStage(s1, _officerId, dt, auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, dt.AddHours(1), auth);

        var p = new ConsensusPolicySnapshot(new ConsensusPolicyId(Guid.NewGuid()), "P", "1", _leaseCaseId, _planId, 1, _ruleSet, ConsensusRuleType.Unanimous, null, _startedAt, new[] {
            new ConsensusParticipantRule(s1, new InstitutionCode("INST"), true, true)
        });

        exec.EvaluateConsensus(new ConsensusAssessmentId(Guid.NewGuid()), p, _startedAt.AddDays(2));
        return exec;
    }

    [Fact]
    public void FinalStage_BlockedBeforeConsensus_Throws()
    {
        var s1 = new WorkflowStageId(Guid.NewGuid());
        var finalS = new WorkflowStageId(Guid.NewGuid());
        var code = new WorkflowStageCode("S");
        var inst = new InstitutionCode("INST");
        var d1 = new WorkflowStageDefinition(s1, code, inst, WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var df = new WorkflowStageDefinition(finalS, code, inst, WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { s1 });
        var def = new WorkflowExecutionDefinition(_planId, 1, _leaseCaseId, _ruleSet, _startedAt.AddDays(-1), _startedAt, new[] { d1, df });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, _startedAt);

        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, inst, new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.StartStage(finalS, _officerId, _startedAt.AddDays(1), auth));
    }

    [Fact]
    public void FinalStage_ReadyAfterConsensus_Succeeds()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        Assert.Equal(WorkflowStageExecutionStatus.InProgress, exec.Stages.First(s => s.Definition.IsFinalDecision).Status);
    }

    [Fact]
    public void FinalDecision_Approval_CompletesExecution()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, _startedAt.AddDays(4), auth);

        Assert.Equal(WorkflowExecutionStatus.Completed, exec.Status);
    }

    [Fact]
    public void FinalDecision_ChangesRequested_Throws()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.ChangesRequested, "R", new[] { "C1" }.ToList().AsReadOnly(), _officerId, _startedAt.AddDays(4), auth));
    }

    [Fact]
    public void FinalDecision_AssessmentId_IsPassedFromExecution()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, _startedAt.AddDays(4), auth);

        var finalStage = exec.Stages.First(s => s.Definition.IsFinalDecision);
        Assert.Equal(exec.ConsensusAssessment!.AssessmentId, finalStage.Decision!.ConsensusAssessmentId);
    }

    [Fact]
    public void NonFinalDecisions_HaveNoAssessmentId()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var nonFinalStage = exec.Stages.First(s => !s.Definition.IsFinalDecision);
        Assert.Null(nonFinalStage.Decision!.ConsensusAssessmentId);
    }

    [Fact]
    public void CompletionEvent_EmittedCorrectly()
    {
        var exec = CreateEvaluatedExecution(out var s1, out var finalS);
        var auth = new VerifiedInstitutionalAuthoritySnapshot(_officerId, new InstitutionCode("INST"), new[] { "C" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString("D")), _startedAt.AddDays(-1), _startedAt, _startedAt.AddDays(5));
        
        exec.StartStage(finalS, _officerId, _startedAt.AddDays(3), auth);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), finalS, WorkflowStageDecisionOutcome.Approved, "R", null, _officerId, _startedAt.AddDays(4), auth);

        var evt = exec.DomainEvents.OfType<WorkflowExecutionCompleted>().Last();
        Assert.Equal(exec.ConsensusAssessment!.AssessmentId, evt.ConsensusAssessmentId);
        Assert.Equal(WorkflowStageDecisionOutcome.Approved, evt.FinalOutcome);
    }
}
