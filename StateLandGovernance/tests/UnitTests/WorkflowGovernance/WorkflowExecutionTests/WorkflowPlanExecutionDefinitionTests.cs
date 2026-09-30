namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using Xunit;
using System.Reflection;

public class WorkflowPlanExecutionDefinitionTests
{
    private VerifiedAuthoritySnapshot Auth(Guid actor, string cap, AuthorityScope scope, DateTime time)
    {
        return new VerifiedAuthoritySnapshot(actor, new[] { cap }, scope, time.AddMinutes(-5), time.AddMinutes(-1), time.AddMinutes(5));
    }

    [Fact]
    public void CreateExecutionDefinition_ApprovedPlan_Succeeds()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { 
                new WorkflowStage(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "Cap", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) 
            }
        );
        var auth = Auth(Guid.NewGuid(), "WorkflowPlanApprover", new AuthorityScope(AuthorityScopeKind.LeaseCase, lc.Value.ToString("D")), created.AddMinutes(5));
        plan.ApprovePlan(auth.ActorId, created.AddMinutes(5), auth);

        var def = plan.CreateExecutionDefinition();
        Assert.NotNull(def);
        Assert.Equal(plan.Id, def.WorkflowPlanId);
        Assert.Single(def.Stages);
    }

    [Fact]
    public void CreateExecutionDefinition_DraftPlan_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { 
                new WorkflowStage(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "Cap", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) 
            }
        );

        Assert.Throws<InvalidWorkflowPlanStateException>(() => plan.CreateExecutionDefinition());
    }

    [Fact]
    public void CreateExecutionDefinition_SupersededPlan_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { 
                new WorkflowStage(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "Cap", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()) 
            }
        );
        var auth = Auth(Guid.NewGuid(), "WorkflowPlanSuperseder", new AuthorityScope(AuthorityScopeKind.LeaseCase, lc.Value.ToString("D")), created.AddMinutes(5));
        plan.SupersedePlan(auth.ActorId, "Reason", created.AddMinutes(5), auth);

        Assert.Throws<InvalidWorkflowPlanStateException>(() => plan.CreateExecutionDefinition());
    }

    [Fact]
    public void Definition_CannotBePubliclyFabricated()
    {
        var ctors = typeof(StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.WorkflowExecutionDefinition).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(ctors);
    }
}
