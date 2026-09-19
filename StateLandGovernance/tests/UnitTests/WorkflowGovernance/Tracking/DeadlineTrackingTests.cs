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
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
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

    private static CompletedWorkflowDecision CreateCompletedWorkflow(
        LeaseCaseId leaseCaseId,
        WorkflowPlan plan,
        WorkflowStageDecisionOutcome outcome = WorkflowStageDecisionOutcome.Approved)
    {
        var finalStage = plan.Stages.Single(s => s.IsFinalDecision);
        return new CompletedWorkflowDecision(
            new WorkflowExecutionId(Guid.NewGuid()),
            plan.Id,
            plan.Revision,
            leaseCaseId,
            new ConsensusAssessmentId(Guid.NewGuid()),
            new WorkflowStageDecisionId(Guid.NewGuid()),
            outcome,
            finalStage.InstitutionCode,
            Guid.NewGuid(),
            DateTime.UtcNow,
            Array.Empty<string>());
    }

    [Fact]
    public void EscalateOverdueTask_Records_Escalation_And_Emits_Event_When_Task_Is_Actually_Overdue()
    {
        var leaseCase = CreateLeaseCase();
        var dueDate = DateTime.UtcNow.AddDays(-14);
        var docReq = leaseCase.AddDocumentSubmissionRequirement(
            new DocumentClassificationCode("SurveyReport"), dueDate);
        var reason = "Survey report submission overdue by 14 days";
        var currentUtc = DateTime.UtcNow;

        Assert.Equal(FulfillmentStatus.Pending, docReq.Status);

        leaseCase.EscalateOverdueTask(docReq.Id, reason, currentUtc);

        Assert.Single(leaseCase.Escalations);
        var escalation = leaseCase.Escalations.First();
        Assert.NotEqual(Guid.Empty, escalation.Id);
        Assert.Equal(leaseCase.Id, escalation.LeaseCaseId);
        Assert.Equal(docReq.Id, escalation.TaskId);
        Assert.Equal(reason, escalation.Reason);
        Assert.Equal(currentUtc, escalation.RequestedAtUtc);

        // Verify status transitioned to Overdue on the requirement
        Assert.Equal(FulfillmentStatus.Overdue, docReq.Status);

        var domainEvent = leaseCase.DomainEvents.OfType<TaskOverdueEscalated>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(leaseCase.Id, domainEvent.LeaseCaseId);
        Assert.Equal(docReq.Id, domainEvent.TaskId);
        Assert.Equal(reason, domainEvent.Reason);
        Assert.Equal(currentUtc, domainEvent.OccurredOn);
    }

    [Fact]
    public void EscalateOverdueTask_Throws_When_Task_Not_Found_On_LeaseCase()
    {
        var leaseCase = CreateLeaseCase();
        var unknownTaskId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidEscalationException>(() =>
            leaseCase.EscalateOverdueTask(unknownTaskId, "Arbitrary task escalation", DateTime.UtcNow));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(leaseCase.Escalations);
    }

    [Fact]
    public void EscalateOverdueTask_Throws_When_Task_Due_Date_Has_Not_Passed()
    {
        var leaseCase = CreateLeaseCase();
        var futureDueDate = DateTime.UtcNow.AddDays(7);
        var docReq = leaseCase.AddDocumentSubmissionRequirement(
            new DocumentClassificationCode("CadastralSurvey"), futureDueDate);

        var ex = Assert.Throws<InvalidEscalationException>(() =>
            leaseCase.EscalateOverdueTask(docReq.Id, "Premature escalation", DateTime.UtcNow));

        Assert.Contains("has not passed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FulfillmentStatus.Pending, docReq.Status);
        Assert.Empty(leaseCase.Escalations);
    }

    [Fact]
    public void EscalateOverdueTask_Throws_When_Task_Already_Fulfilled()
    {
        var leaseCase = CreateLeaseCase();
        var pastDueDate = DateTime.UtcNow.AddDays(-5);
        var docReq = leaseCase.AddDocumentSubmissionRequirement(
            new DocumentClassificationCode("CadastralSurvey"), pastDueDate);
        leaseCase.FulfillDocumentRequirement(docReq.Id, new GovernedDocumentId(Guid.NewGuid()), DateTime.UtcNow.AddDays(-2));

        Assert.Equal(FulfillmentStatus.Fulfilled, docReq.Status);

        var ex = Assert.Throws<InvalidEscalationException>(() =>
            leaseCase.EscalateOverdueTask(docReq.Id, "Already fulfilled task", DateTime.UtcNow));

        Assert.Contains("already Fulfilled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(leaseCase.Escalations);
    }

    [Fact]
    public void EscalateOverdueTask_Throws_If_Case_Is_Already_HandedOff()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);

        leaseCase.GenerateHandoffPackage();
        Assert.Equal(LeaseCaseStatus.HandedOff, leaseCase.Status);

        var stageId = plan.Stages.First().Id.Value;
        var ex = Assert.Throws<InvalidEscalationException>(() =>
            leaseCase.EscalateOverdueTask(stageId, "Task overdue", DateTime.UtcNow));

        Assert.Contains("handed off", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deadline_Expiry_Does_Not_Mutate_Approval_Status()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        var docReq = leaseCase.AddDocumentSubmissionRequirement(
            new DocumentClassificationCode("CadastralSurvey"),
            DateTime.UtcNow.AddDays(-7)); // Overdue by 7 days

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        // Because of the pending/overdue document requirement, case status is ApprovedWithConditions, NOT ReadyForHandoff
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);

        var initialPlanStatus = plan.Status;

        // Escalate overdue task
        leaseCase.EscalateOverdueTask(docReq.Id, "Cadastral survey is 7 days overdue", DateTime.UtcNow);

        // Explicitly verify DAG and Case statuses remain untouched (NO automatic approval, NO handoff transition)
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);
        Assert.Equal(initialPlanStatus, plan.Status);
        Assert.Equal(WorkflowPlanStatus.Approved, plan.Status);
        Assert.Equal(FulfillmentStatus.Overdue, docReq.Status);
        Assert.Null(docReq.FulfilledByDocumentId);
        Assert.Null(docReq.FulfilledAtUtc);
    }
}
