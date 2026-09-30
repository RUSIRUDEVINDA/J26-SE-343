namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowPlanningTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Events;
using Xunit;
using System.Reflection;

public class WorkflowPlanLifecycleTests
{
    private WorkflowStage CreateFinalStage() => new WorkflowStage(
        new WorkflowStageId(Guid.NewGuid()),
        new WorkflowStageCode("F"),
        new InstitutionCode("I"),
        WorkflowStageType.FinalDecision,
        "Cap",
        new RoutingReasonCode("R"),
        "D",
        null,
        true,
        Array.Empty<WorkflowStageId>()
    );

    private WorkflowRecommendationReference CreateRec() => new WorkflowRecommendationReference(
        "REC1", new AnalysisModelReference("P", "m1", "v1"), DateTime.UtcNow, null
    );

    private VerifiedAuthoritySnapshot CreateAuthority(Guid leaseCaseId, Guid actorId, string capability, DateTime t) => new VerifiedAuthoritySnapshot(
        actorId, new[] { capability }, new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.ToString("D")), t.AddMinutes(-5), t.AddMinutes(-1), t.AddMinutes(5)
    );

    [Fact]
    public void ValidConstruction_Manual_DraftState()
    {
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            WorkflowPlanSource.Manual,
            new VerifiedFactSnapshotId(Guid.NewGuid()),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("R", "1"),
            null,
            DateTime.UtcNow,
            new[] { CreateFinalStage() }
        );
        
        Assert.Equal(WorkflowPlanStatus.Draft, plan.Status);
        Assert.Equal(1, plan.Revision);
        Assert.Single(plan.DomainEvents.OfType<WorkflowPlanCreated>());
    }

    [Fact]
    public void ValidConstruction_MachineRecommended_RequiresRecommendation()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.MachineRecommended, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, DateTime.UtcNow, new[] { CreateFinalStage() }
        ));

        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.MachineRecommended, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), CreateRec(), DateTime.UtcNow, new[] { CreateFinalStage() }
        );
        Assert.NotNull(plan.RecommendationReference);
    }

    [Fact]
    public void ValidApproval()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);

        plan.ApprovePlan(actor, approvedAt, auth);

        Assert.Equal(WorkflowPlanStatus.Approved, plan.Status);
        Assert.Equal(2, plan.Revision);
        Assert.Equal(approvedAt, plan.ApprovedAt);
        var ev = plan.DomainEvents.OfType<WorkflowPlanApproved>().Single();
        Assert.Equal(actor, ev.ApprovingActorId);
    }

    [Fact]
    public void Approval_WrongCapability_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WrongCap", approvedAt);

        Assert.Throws<MissingVerifiedAuthorityException>(() => plan.ApprovePlan(actor, approvedAt, auth));
    }

    [Fact]
    public void Approval_BeforeCreatedAt_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow;
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(-1);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.ApprovePlan(actor, approvedAt, auth));
    }

    [Fact]
    public void RepeatedApproval_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);

        plan.ApprovePlan(actor, approvedAt, auth);
        Assert.Throws<WorkflowPlanAlreadyApprovedException>(() => plan.ApprovePlan(actor, approvedAt, auth));
    }

    [Fact]
    public void Supersession_FromDraft_Succeeds()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var supersededAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);

        plan.SupersedePlan(actor, "Reason", supersededAt, auth);

        Assert.Equal(WorkflowPlanStatus.Superseded, plan.Status);
        Assert.Equal(2, plan.Revision);
    }

    [Fact]
    public void Supersession_FromApproved_Succeeds()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(2);
        var auth1 = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);
        plan.ApprovePlan(actor, approvedAt, auth1);

        var supersededAt = created.AddMinutes(5);
        var auth2 = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);
        plan.SupersedePlan(actor, "Reason", supersededAt, auth2);

        Assert.Equal(WorkflowPlanStatus.Superseded, plan.Status);
        Assert.Equal(3, plan.Revision);
    }

    [Fact]
    public void Supersession_InvalidReason_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, DateTime.UtcNow, new[] { CreateFinalStage() }
        );
        var auth = CreateAuthority(lc.Value, Guid.NewGuid(), "WorkflowPlanSuperseder", DateTime.UtcNow);

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.SupersedePlan(Guid.NewGuid(), "", DateTime.UtcNow, auth));
        Assert.Throws<InvalidWorkflowPlanException>(() => plan.SupersedePlan(Guid.NewGuid(), "A\nB", DateTime.UtcNow, auth));
    }

    [Fact]
    public void Supersession_BeforeApprovedAt_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(5);
        var auth1 = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);
        plan.ApprovePlan(actor, approvedAt, auth1);

        var supersededAt = created.AddMinutes(2); // Before approval!
        var auth2 = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);
        Assert.Throws<InvalidWorkflowPlanException>(() => plan.SupersedePlan(actor, "Reason", supersededAt, auth2));
    }

    [Fact]
    public void RepeatedSupersession_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var supersededAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);
        plan.SupersedePlan(actor, "Reason", supersededAt, auth);

        Assert.Throws<WorkflowPlanAlreadySupersededException>(() => plan.SupersedePlan(actor, "Reason", supersededAt, auth));
    }

    [Fact]
    public void Approve_Superseded_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var supersededAt = created.AddMinutes(5);
        var auth1 = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);
        plan.SupersedePlan(actor, "Reason", supersededAt, auth1);

        var auth2 = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", supersededAt);
        Assert.Throws<InvalidWorkflowPlanStateException>(() => plan.ApprovePlan(actor, supersededAt, auth2));
    }

    [Fact]
    public void RevisionOverflow_Atomicity()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var prop = typeof(WorkflowPlan).GetProperty("Revision", BindingFlags.Public | BindingFlags.Instance);
        prop!.SetValue(plan, int.MaxValue);

        var actor = Guid.NewGuid();
        var approvedAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanApprover", approvedAt);

        Assert.Throws<WorkflowPlanRevisionOverflowException>(() => plan.ApprovePlan(actor, approvedAt, auth));
        
        Assert.Equal(WorkflowPlanStatus.Draft, plan.Status);
        Assert.Equal(int.MaxValue, plan.Revision);
    }

    [Fact]
    public void Construction_RecommendationGeneratedAtAfterCreatedAt_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var rec = new WorkflowRecommendationReference("REC", new AnalysisModelReference("P", "m", "v"), created.AddMinutes(5), null);

        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.MachineRecommended, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), rec, created, new[] { CreateFinalStage() }
        ));
    }

    [Fact]
    public void SupersededAt_AndReason_ArePersisted()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.NewGuid();
        var supersededAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, actor, "WorkflowPlanSuperseder", supersededAt);
        plan.SupersedePlan(actor, "  Canonical Reason  ", supersededAt, auth);

        Assert.Equal(supersededAt, plan.SupersededAt);
        Assert.Equal("Canonical Reason", plan.SupersessionReason);
    
    }
    
    [Fact]
    public void ApprovePlan_EmptyActorId_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        var actor = Guid.Empty;
        var approvedAt = created.AddMinutes(5);
        var auth = CreateAuthority(lc.Value, Guid.NewGuid(), "WorkflowPlanApprover", approvedAt);

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.ApprovePlan(actor, approvedAt, auth));
    }
    
    [Fact]
    public void SupersedePlan_EmptyActorId_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, DateTime.UtcNow, new[] { CreateFinalStage() }
        );

        var actor = Guid.Empty;
        var auth = CreateAuthority(lc.Value, Guid.NewGuid(), "WorkflowPlanSuperseder", DateTime.UtcNow);

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.SupersedePlan(actor, "Reason", DateTime.UtcNow, auth));
    
    }
    
    [Fact]
    public void ApprovePlan_NullAuthority_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var created = DateTime.UtcNow.AddMinutes(-10);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, created, new[] { CreateFinalStage() }
        );

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.ApprovePlan(Guid.NewGuid(), created.AddMinutes(5), null!));
    }
    
    [Fact]
    public void SupersedePlan_NullAuthority_Throws()
    {
        var lc = new LeaseCaseId(Guid.NewGuid());
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()), lc, WorkflowPlanSource.Manual, 
            new VerifiedFactSnapshotId(Guid.NewGuid()), new DocumentCompletenessAssessmentId(Guid.NewGuid()), 
            new WorkflowRuleSetReference("R", "1"), null, DateTime.UtcNow, new[] { CreateFinalStage() }
        );

        Assert.Throws<InvalidWorkflowPlanException>(() => plan.SupersedePlan(Guid.NewGuid(), "Reason", DateTime.UtcNow, null!));
    }

}
