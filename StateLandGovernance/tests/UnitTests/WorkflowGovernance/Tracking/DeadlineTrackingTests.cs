namespace StateLandGovernance.UnitTests.WorkflowGovernance.Tracking;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Tracking;
using StateLandGovernance.WorkflowGovernance.Domain.Tracking.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class DeadlineTrackingTests
{
    private static LeaseCase CreateLeaseCase()
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

        return new LeaseCase(leaseCaseId, "APP-TRACK-001", actorId, actionTime, authority);
    }

    private static (LeaseCase LeaseCase, WorkflowPlan Plan) CreateLeaseCaseWithApprovedPlan()
    {
        var actorId = Guid.NewGuid();
        var leaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var actionTime = DateTime.UtcNow.AddHours(-1);
        var snapshotId = Guid.NewGuid();

        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString("D"));
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator", "WorkflowPlanApprover" },
            scope,
            actionTime.AddMinutes(-10),
            actionTime.AddMinutes(-5),
            actionTime.AddHours(2));

        var leaseCase = new LeaseCase(leaseCaseId, "APP-HANDOFF-001", actorId, actionTime, authority, null, snapshotId);

        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            leaseCaseId,
            WorkflowPlanSource.Manual,
            new VerifiedFactSnapshotId(snapshotId),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STANDARD", "1.0"),
            null,
            actionTime.AddMinutes(5),
            new[]
            {
                new WorkflowStage(
                    new WorkflowStageId(Guid.NewGuid()),
                    new WorkflowStageCode("FINAL"),
                    new InstitutionCode("LC"),
                    WorkflowStageType.FinalDecision,
                    "WorkflowPlanApprover",
                    new RoutingReasonCode("STANDARD_FLOW"),
                    "Final Decision Stage",
                    null,
                    true,
                    Array.Empty<WorkflowStageId>())
            });

        leaseCase.SetInitialWorkflowPlan(plan);
        plan.ApprovePlan(actorId, actionTime.AddMinutes(15), authority);

        return (leaseCase, plan);
    }

    [Fact]
    public void EscalateOverdueTask_Records_Escalation_And_Emits_Event()
    {
        var leaseCase = CreateLeaseCase();
        var taskId = Guid.NewGuid();
        var reason = "Survey report submission overdue by 14 days";
        var currentUtc = DateTime.UtcNow;

        leaseCase.EscalateOverdueTask(taskId, reason, currentUtc);

        Assert.Single(leaseCase.Escalations);
        var escalation = leaseCase.Escalations.First();
        Assert.NotEqual(Guid.Empty, escalation.Id);
        Assert.Equal(leaseCase.Id, escalation.LeaseCaseId);
        Assert.Equal(taskId, escalation.TaskId);
        Assert.Equal(reason, escalation.Reason);
        Assert.Equal(currentUtc, escalation.RequestedAtUtc);

        var domainEvent = leaseCase.DomainEvents.OfType<TaskOverdueEscalated>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(leaseCase.Id, domainEvent.LeaseCaseId);
        Assert.Equal(taskId, domainEvent.TaskId);
        Assert.Equal(reason, domainEvent.Reason);
        Assert.Equal(currentUtc, domainEvent.OccurredOn);
    }

    [Fact]
    public void EscalateOverdueTask_Throws_If_Case_Is_Already_HandedOff()
    {
        var (leaseCase, _) = CreateLeaseCaseWithApprovedPlan();
        leaseCase.EvaluateOverallReadiness();
        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);

        leaseCase.GenerateHandoffPackage();
        Assert.Equal(LeaseCaseStatus.HandedOff, leaseCase.Status);

        var taskId = Guid.NewGuid();
        var ex = Assert.Throws<InvalidEscalationException>(() =>
            leaseCase.EscalateOverdueTask(taskId, "Task overdue", DateTime.UtcNow));

        Assert.Contains("handed off", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deadline_Expiry_Does_Not_Mutate_Approval_Status()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        var docReq = leaseCase.AddDocumentSubmissionRequirement(
            new DocumentClassificationCode("CadastralSurvey"),
            DateTime.UtcNow.AddDays(-7)); // Overdue by 7 days

        leaseCase.EvaluateOverallReadiness();
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);

        var initialPlanStatus = plan.Status;
        var initialConditionStatus = docReq.Status;

        // Escalate overdue task
        leaseCase.EscalateOverdueTask(docReq.Id, "Cadastral survey is 7 days overdue", DateTime.UtcNow);

        // Explicitly verify DAG and Case statuses remain untouched (NO automatic approval)
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);
        Assert.Equal(initialPlanStatus, plan.Status);
        Assert.Equal(WorkflowPlanStatus.Approved, plan.Status);
        Assert.Equal(initialConditionStatus, docReq.Status);
        Assert.Equal(FulfillmentStatus.Pending, docReq.Status);
        Assert.Null(docReq.FulfilledByDocumentId);
        Assert.Null(docReq.FulfilledAtUtc);
    }
}
