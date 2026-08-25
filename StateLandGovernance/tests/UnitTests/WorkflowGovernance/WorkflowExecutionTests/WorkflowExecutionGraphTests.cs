namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowExecutionGraphTests
{
    private VerifiedInstitutionalAuthoritySnapshot Auth(Guid actor, InstitutionCode inst, string cap, AuthorityScope scope, DateTime time)
    {
        return new VerifiedInstitutionalAuthoritySnapshot(actor, inst, new[] { cap }, scope, time.AddMinutes(-5), time, time.AddMinutes(5));
    }

    [Fact]
    public void SinglePrerequisite_Unlock()
    {
        var s1Id = new WorkflowStageId(Guid.NewGuid());
        var s2Id = new WorkflowStageId(Guid.NewGuid());

        var s1 = new WorkflowStageDefinition(s1Id, new WorkflowStageCode("C1"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(s2Id, new WorkflowStageCode("C2"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, new[] { s1Id });
        
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { s1, s2, new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var actor = Guid.NewGuid();
        var time = DateTime.UtcNow.AddMinutes(1);
        var auth = Auth(actor, new InstitutionCode("I"), "Cap", new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D")), time);

        exec.StartStage(s1Id, actor, time, auth);
        
        var time2 = time.AddMinutes(1);
        var auth2 = Auth(actor, new InstitutionCode("I"), "Cap", new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D")), time2);
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1Id, WorkflowStageDecisionOutcome.Approved, "Reason", null, actor, time2, auth2);

        var stage2 = exec.Stages.Single(x => x.Definition.StageId == s2Id);
        Assert.Equal(WorkflowStageExecutionStatus.Ready, stage2.Status);
    }
    
    [Fact]
    public void MultiplePrerequisites_UnlockAfterAllComplete()
    {
        var s1Id = new WorkflowStageId(Guid.NewGuid());
        var s2Id = new WorkflowStageId(Guid.NewGuid());
        var s3Id = new WorkflowStageId(Guid.NewGuid());

        var s1 = new WorkflowStageDefinition(s1Id, new WorkflowStageCode("C1"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(s2Id, new WorkflowStageCode("C2"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s3 = new WorkflowStageDefinition(s3Id, new WorkflowStageCode("C3"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, new[] { s1Id, s2Id });
        
        var def = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1, new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("R", "1"), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, new List<WorkflowStageDefinition> { s1, s2, s3, new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) }.AsReadOnly());
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var actor = Guid.NewGuid();
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, def.LeaseCaseId.Value.ToString("D"));
        var time = DateTime.UtcNow.AddMinutes(1);
        
        exec.StartStage(s1Id, actor, time, Auth(actor, new InstitutionCode("I"), "Cap", scope, time));
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s1Id, WorkflowStageDecisionOutcome.Approved, "R", null, actor, time.AddMinutes(1), Auth(actor, new InstitutionCode("I"), "Cap", scope, time.AddMinutes(1)));

        // s3 should still be blocked
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, exec.Stages.Single(x => x.Definition.StageId == s3Id).Status);

        exec.StartStage(s2Id, actor, time.AddMinutes(2), Auth(actor, new InstitutionCode("I"), "Cap", scope, time.AddMinutes(2)));
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s2Id, WorkflowStageDecisionOutcome.Approved, "R", null, actor, time.AddMinutes(3), Auth(actor, new InstitutionCode("I"), "Cap", scope, time.AddMinutes(3)));
        
        // s3 should now be ready and Execution should NOT be AwaitingConsensus yet
        Assert.Equal(WorkflowStageExecutionStatus.Ready, exec.Stages.Single(x => x.Definition.StageId == s3Id).Status);
        Assert.Equal(WorkflowExecutionStatus.Active, exec.Status);
        
        exec.StartStage(s3Id, actor, time.AddMinutes(4), Auth(actor, new InstitutionCode("I"), "Cap", scope, time.AddMinutes(4)));
        exec.RecordStageDecision(new WorkflowStageDecisionId(Guid.NewGuid()), s3Id, WorkflowStageDecisionOutcome.Approved, "R", null, actor, time.AddMinutes(5), Auth(actor, new InstitutionCode("I"), "Cap", scope, time.AddMinutes(5)));
        
        // Now it's awaiting consensus
        Assert.Equal(WorkflowExecutionStatus.AwaitingConsensus, exec.Status);
    }
}



