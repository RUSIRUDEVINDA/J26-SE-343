namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowPlanning;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Routing;
using Xunit;

public class InstitutionalRoutingTests
{
    private static LeaseCase CreateLeaseCase(Guid? currentSnapshotId = null)
    {
        var actorId = Guid.NewGuid();
        var leaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var actionTime = DateTime.UtcNow;
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString("D"));
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            scope,
            actionTime.AddMinutes(-5),
            actionTime.AddMinutes(-2),
            actionTime.AddMinutes(10));

        var leaseCase = new LeaseCase(leaseCaseId, "APP-123", actorId, actionTime, authority);
        if (currentSnapshotId.HasValue)
        {
            leaseCase.SetCurrentVerifiedFactSnapshot(currentSnapshotId.Value);
        }

        return leaseCase;
    }

    private static VerifiedAuthoritySnapshot CreatePlannerAuthority(LeaseCaseId leaseCaseId, Guid actorId)
    {
        var actionTime = DateTime.UtcNow;
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString("D"));
        return new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "WorkflowPlanSuperseder", "WorkflowPlanner" },
            scope,
            actionTime.AddMinutes(-10),
            actionTime.AddMinutes(-2),
            actionTime.AddMinutes(30));
    }

    [Fact]
    public void InstitutionalRoutingEngine_Generates_Valid_DAG_With_Divisional_Secretariat_As_Bottleneck()
    {
        // 1. Generate plan with 2 standard institutions ("UDA", "CEA") and requiresCommissionerApproval = true
        var institutions = new[]
        {
            new InstitutionCode("UDA"),
            new InstitutionCode("CEA")
        };

        var stages = InstitutionalRoutingEngine.GeneratePlanStages(institutions, requiresCommissionerApproval: true);

        // 2. Assert that the returned collection has exactly 4 stages
        Assert.Equal(4, stages.Count);

        var dsStage = stages.Single(s => s.InstitutionCode == InstitutionalRoutingEngine.DivisionalSecretariat);
        var commissionerStage = stages.Single(s => s.InstitutionCode == InstitutionalRoutingEngine.LandCommissioner);
        var standardStages = stages.Where(s => s.InstitutionCode != InstitutionalRoutingEngine.DivisionalSecretariat &&
                                               s.InstitutionCode != InstitutionalRoutingEngine.LandCommissioner).ToList();

        Assert.Equal(2, standardStages.Count);
        foreach (var standard in standardStages)
        {
            Assert.Empty(standard.Prerequisites);
            Assert.False(standard.IsFinalDecision);
        }

        // 3. Assert the DS stage has 2 prerequisites (the standard ones)
        Assert.Equal(2, dsStage.Prerequisites.Count);
        foreach (var standard in standardStages)
        {
            Assert.Contains(standard.Id, dsStage.Prerequisites);
        }
        Assert.False(dsStage.IsFinalDecision);

        // 4. Assert the Commissioner stage has exactly 1 prerequisite (the DS stage) and IsFinalDecision == true
        Assert.Single(commissionerStage.Prerequisites);
        Assert.Contains(dsStage.Id, commissionerStage.Prerequisites);
        Assert.True(commissionerStage.IsFinalDecision);
        Assert.Equal(WorkflowStageType.FinalDecision, commissionerStage.StageType);

        // 5. Verify that this collection forms a completely valid WorkflowPlan (DAG passes all graph checks)
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            WorkflowPlanSource.RuleBased,
            new VerifiedFactSnapshotId(Guid.NewGuid()),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STD_RULES", "v1.0"),
            null,
            DateTime.UtcNow,
            stages);

        Assert.Equal(WorkflowPlanStatus.Draft, plan.Status);
        Assert.Equal(4, plan.Stages.Count);
    }

    [Fact]
    public void LeaseCase_ReviseWorkflowPlan_Supersedes_Active_Plan_And_Increments_Revision()
    {
        var snapshotId = Guid.NewGuid();
        var leaseCase = CreateLeaseCase(snapshotId);
        var actorId = Guid.NewGuid();
        var authority = CreatePlannerAuthority(leaseCase.Id, actorId);

        // 1. Set up an initial plan
        var stages1 = InstitutionalRoutingEngine.GeneratePlanStages(new[] { new InstitutionCode("UDA") }, false);
        var initialPlan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            leaseCase.Id,
            WorkflowPlanSource.RuleBased,
            new VerifiedFactSnapshotId(snapshotId),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STD_RULES", "v1.0"),
            null,
            DateTime.UtcNow,
            stages1);

        leaseCase.SetInitialWorkflowPlan(initialPlan);
        Assert.Same(initialPlan, leaseCase.ActiveWorkflowPlan);
        Assert.Equal(1, leaseCase.Revision);
        Assert.Null(initialPlan.SupersededAt);

        // 2. Call ReviseWorkflowPlan with a new plan
        var stages2 = InstitutionalRoutingEngine.GeneratePlanStages(new[] { new InstitutionCode("UDA"), new InstitutionCode("CEA") }, true);
        var newPlan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            leaseCase.Id,
            WorkflowPlanSource.RuleBased,
            new VerifiedFactSnapshotId(snapshotId),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STD_RULES", "v2.0"),
            null,
            DateTime.UtcNow,
            stages2);

        leaseCase.ReviseWorkflowPlan(newPlan, authority, "Expanding review to include CEA and Commissioner approval");

        // 3. Assert Revision is 2, ActiveWorkflowPlan is the new plan, and the old plan's SupersededAt is populated
        Assert.Equal(2, leaseCase.Revision);
        Assert.Same(newPlan, leaseCase.ActiveWorkflowPlan);
        Assert.NotNull(initialPlan.SupersededAt);
        Assert.Equal(WorkflowPlanStatus.Superseded, initialPlan.Status);
        Assert.Equal("Expanding review to include CEA and Commissioner approval", initialPlan.SupersessionReason);
        Assert.Equal(WorkflowPlanStatus.Draft, newPlan.Status);
        Assert.Equal(2, leaseCase.WorkflowPlanHistory.Count);
    }

    [Fact]
    public void LeaseCase_ReviseWorkflowPlan_Fails_If_FactSnapshot_Mismatch()
    {
        var currentSnapshotId = Guid.NewGuid();
        var mismatchedSnapshotId = Guid.NewGuid();

        var leaseCase = CreateLeaseCase(currentSnapshotId);
        var authority = CreatePlannerAuthority(leaseCase.Id, Guid.NewGuid());

        var stages = InstitutionalRoutingEngine.GeneratePlanStages(new[] { new InstitutionCode("UDA") }, false);
        var planWithMismatch = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            leaseCase.Id,
            WorkflowPlanSource.RuleBased,
            new VerifiedFactSnapshotId(mismatchedSnapshotId),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STD_RULES", "v1.0"),
            null,
            DateTime.UtcNow,
            stages);

        var ex = Assert.Throws<InvalidWorkflowPlanException>(() =>
            leaseCase.ReviseWorkflowPlan(planWithMismatch, authority, "Mismatch test"));

        Assert.Contains("VerifiedFactSnapshotId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
