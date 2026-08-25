namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowStageTransitionTests
{
    private VerifiedInstitutionalAuthoritySnapshot Auth(Guid actor, InstitutionCode inst, string cap, AuthorityScope scope, DateTime time)
    {
        return new VerifiedInstitutionalAuthoritySnapshot(actor, inst, new[] { cap }, scope, time.AddMinutes(-5), time, time.AddMinutes(5));
    }

    [Fact]
    public void StartStage_ValidReadyStage_TransitionsToInProgress()
    {
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { s1, new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var actor = Guid.NewGuid();
        var time = DateTime.UtcNow.AddMinutes(1);
        var auth = Auth(actor, new InstitutionCode("I"), "Cap", new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D")), time);

        exec.StartStage(s1.StageId, actor, time, auth);

        var stage = exec.Stages.First(x => !x.Definition.IsFinalDecision);
        Assert.Equal(WorkflowStageExecutionStatus.InProgress, stage.Status);
        Assert.Equal(actor, stage.ActingOfficerId);
        Assert.Equal(time, stage.StartedAt);
        Assert.Equal(2, exec.Revision);
        Assert.Single(exec.DomainEvents.OfType<WorkflowStageStarted>());
    }

    [Fact]
    public void StartStage_BlockedStage_ThrowsInvalidTransition()
    {
        var s1 = new WorkflowStageId(Guid.NewGuid());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, new[] { s1 });
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { s2, new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var actor = Guid.NewGuid();
        var time = DateTime.UtcNow.AddMinutes(1);
        var auth = Auth(actor, new InstitutionCode("I"), "Cap", new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D")), time);

        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.StartStage(s2.StageId, actor, time, auth));
    }

    [Fact]
    public void StartStage_NullAuthority_Throws()
    {
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { s1, new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        Assert.Throws<InvalidWorkflowExecutionException>(() => exec.StartStage(s1.StageId, Guid.NewGuid(), DateTime.UtcNow, null!));
    }
    
    [Fact]
    public void StartStage_UnknownStage_ThrowsNotFound()
    {
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>()), new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        Assert.Throws<WorkflowStageExecutionNotFoundException>(() => exec.StartStage(new WorkflowStageId(Guid.NewGuid()), Guid.NewGuid(), DateTime.UtcNow, Auth(Guid.NewGuid(), new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, "L"), DateTime.UtcNow)));
    }
    
    [Fact]
    public void StartStage_EmptyActor_Throws()
    {
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>()), new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        Assert.Throws<InvalidWorkflowExecutionException>(() => exec.StartStage(new WorkflowStageId(Guid.NewGuid()), Guid.Empty, DateTime.UtcNow, Auth(Guid.NewGuid(), new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, "L"), DateTime.UtcNow)));
    }
    
    [Fact]
    public void StartStage_NonUtcTimestamp_Throws()
    {
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>()), new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        Assert.Throws<InvalidWorkflowExecutionException>(() => exec.StartStage(new WorkflowStageId(Guid.NewGuid()), Guid.NewGuid(), DateTime.Now, Auth(Guid.NewGuid(), new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, "L"), DateTime.UtcNow)));
    }
    
    [Fact]
    public void StartStage_FinalDecisionStage_ThrowsInvalidTransition()
    {
        var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "Cap", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>()), sf }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var actor = Guid.NewGuid();
        var time = DateTime.UtcNow.AddMinutes(1);
        var auth = Auth(actor, new InstitutionCode("I"), "Cap", new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D")), time);

        Assert.Throws<InvalidWorkflowStageTransitionException>(() => exec.StartStage(sf.StageId, actor, time, auth));
    }
    [Fact]
    public void FinalDecisionRetry_AfterAwaitingConsensus_ExactData_ThrowsDuplicate()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, lc, new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new[] { s1, s2 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        var time = DateTime.UtcNow.AddMinutes(1);
        var actor = Guid.NewGuid();
        var auth = Auth(actor, new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, lc.Value.ToString("D")), time);
        
        exec.StartStage(s1.StageId, actor, time, auth);
        
        var did = new WorkflowStageDecisionId(Guid.NewGuid());
        exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(1), auth);
        
        Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
        
        Assert.Throws<DuplicateWorkflowStageDecisionException>(() => exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(1), auth));
    }

    [Fact]
    public void FinalDecisionRetry_AfterAwaitingConsensus_ChangedData_ThrowsConflicting()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, lc, new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new[] { s1, s2 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        var time = DateTime.UtcNow.AddMinutes(1);
        var actor = Guid.NewGuid();
        var auth = Auth(actor, new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, lc.Value.ToString("D")), time);
        
        exec.StartStage(s1.StageId, actor, time, auth);
        
        var did = new WorkflowStageDecisionId(Guid.NewGuid());
        exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(1), auth);
        
        Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
        
        Assert.Throws<ConflictingWorkflowStageDecisionException>(() => exec.RecordStageDecision(did, s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(2), auth));
    }

    [Fact]
    public void NewDecisionId_AfterAwaitingConsensus_ForCompletedStage_ThrowsAlreadyDecided()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, lc, new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new[] { s1, s2 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);
        
        var time = DateTime.UtcNow.AddMinutes(1);
        var actor = Guid.NewGuid();
        var auth = Auth(actor, new InstitutionCode("I"), "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, lc.Value.ToString("D")), time);
        
        exec.StartStage(s1.StageId, actor, time, auth);
        
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(1), auth);
        
        Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
        
        Assert.Throws<WorkflowStageAlreadyDecidedException>(() => exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1.StageId, WorkflowStageDecisionOutcome.Approved, null, null, actor, time.AddMinutes(2), auth));
    }
}
