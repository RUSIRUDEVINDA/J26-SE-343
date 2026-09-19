namespace StateLandGovernance.UnitTests.WorkflowGovernance.Handoff;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using Xunit;

public class HandoffCoordinationTests
{
    private static VerifiedAuthoritySnapshot CreateAuthority(Guid actor, string cap, AuthorityScope scope, DateTime time)
    {
        return new VerifiedAuthoritySnapshot(actor, new[] { cap }, scope, time.AddMinutes(-5), time.AddMinutes(-1), time.AddMinutes(5));
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
    public void LeaseCase_Initial_Status_Is_Draft()
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

        var leaseCase = new LeaseCase(leaseCaseId, "APP-001", actorId, actionTime, authority);

        Assert.Equal(LeaseCaseStatus.Draft, leaseCase.Status);
        Assert.Null(leaseCase.HandoffPackage);
    }

    [Fact]
    public void LeaseCase_EvaluateOverallReadiness_When_Plan_Draft_Transitions_To_InWorkflow()
    {
        var actorId = Guid.NewGuid();
        var leaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var actionTime = DateTime.UtcNow.AddMinutes(-10);
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString("D"));
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            scope,
            actionTime.AddMinutes(-5),
            actionTime.AddMinutes(-2),
            actionTime.AddMinutes(30));

        var leaseCase = new LeaseCase(leaseCaseId, "APP-002", actorId, actionTime, authority);
        var plan = new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            leaseCaseId,
            WorkflowPlanSource.Manual,
            new VerifiedFactSnapshotId(Guid.NewGuid()),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("STANDARD", "1.0"),
            null,
            actionTime.AddMinutes(1),
            new[]
            {
                new WorkflowStage(
                    new WorkflowStageId(Guid.NewGuid()),
                    new WorkflowStageCode("STAGE_1"),
                    new InstitutionCode("UDA"),
                    WorkflowStageType.FinalDecision,
                    "Assessor",
                    new RoutingReasonCode("STANDARD_FLOW"),
                    "Stage 1",
                    null,
                    true,
                    Array.Empty<WorkflowStageId>())
            });

        leaseCase.SetInitialWorkflowPlan(plan);
        leaseCase.EvaluateOverallReadiness();

        Assert.Equal(LeaseCaseStatus.InWorkflow, leaseCase.Status);
    }

    [Fact]
    public void LeaseCase_Approved_Plan_Without_Completed_Execution_Remains_InWorkflow()
    {
        var (leaseCase, _) = CreateLeaseCaseWithApprovedPlan();

        leaseCase.EvaluateOverallReadiness();

        // An approved plan is only a blueprint; execution is incomplete so status MUST remain InWorkflow
        Assert.Equal(LeaseCaseStatus.InWorkflow, leaseCase.Status);
        Assert.Null(leaseCase.CompletedWorkflow);
    }

    [Fact]
    public void LeaseCase_Transitions_To_ApprovedWithConditions_When_Execution_Completed_With_Pending_Approval_Condition()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        leaseCase.AddApprovalCondition(new InstitutionCode("CEA"), "Install water treatment facility");

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);
        Assert.NotNull(leaseCase.CompletedWorkflow);
    }

    [Fact]
    public void LeaseCase_Transitions_To_ApprovedWithConditions_When_Execution_Completed_With_Pending_Document_Requirement()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        leaseCase.AddDocumentSubmissionRequirement(new DocumentClassificationCode("CadastralSurvey"), DateTime.UtcNow.AddDays(14));

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);
    }

    [Fact]
    public void LeaseCase_Transitions_To_ReadyForHandoff_When_Execution_Completed_And_Zero_Conditions_Or_Requirements()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);
    }

    [Fact]
    public void LeaseCase_Transitions_To_ReadyForHandoff_When_Execution_Completed_And_All_Conditions_Fulfilled()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        var condition = leaseCase.AddApprovalCondition(new InstitutionCode("CEA"), "Install water treatment facility");
        var docReq = leaseCase.AddDocumentSubmissionRequirement(new DocumentClassificationCode("CadastralSurvey"), DateTime.UtcNow.AddDays(14));

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);

        leaseCase.FulfillCondition(condition.Id, DateTime.UtcNow);
        leaseCase.FulfillDocumentRequirement(docReq.Id, new GovernedDocumentId(Guid.NewGuid()), DateTime.UtcNow);

        leaseCase.EvaluateOverallReadiness();
        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);
    }

    [Fact]
    public void LeaseCase_Does_Not_Become_Ready_When_Execution_Completed_With_Rejection()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();

        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Rejected);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.InWorkflow, leaseCase.Status);
        Assert.NotEqual(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);
    }

    [Fact]
    public void LeaseCase_GenerateHandoffPackage_Throws_When_Not_In_ReadyForHandoff_Status()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        leaseCase.AddApprovalCondition(new InstitutionCode("CEA"), "Pending condition");
        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, leaseCase.Status);
        var ex = Assert.Throws<InvalidHandoffException>(() => leaseCase.GenerateHandoffPackage());
        Assert.Contains("ReadyForHandoff", ex.Message);
    }

    [Fact]
    public void LeaseCase_GenerateHandoffPackage_Succeeds_And_Transitions_To_HandedOff()
    {
        var (leaseCase, plan) = CreateLeaseCaseWithApprovedPlan();
        var completedWorkflow = CreateCompletedWorkflow(leaseCase.Id, plan, WorkflowStageDecisionOutcome.Approved);
        leaseCase.RecordCompletedWorkflow(completedWorkflow);

        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, leaseCase.Status);

        var package = leaseCase.GenerateHandoffPackage();

        Assert.NotNull(package);
        Assert.Equal(leaseCase.Id, package.LeaseCaseId);
        Assert.Equal(plan.Id.Value, package.FinalWorkflowPlanId);
        Assert.Equal(leaseCase.CurrentVerifiedFactSnapshotId, package.VerifiedFactSnapshotId);
        Assert.Equal(completedWorkflow.FinalDecisionId.Value, package.FinalDecisionId);
        Assert.NotNull(package.FulfilledDocumentIds);
        Assert.Null(package.DownstreamAcknowledgementId);
        Assert.Equal(package, leaseCase.HandoffPackage);
        Assert.Equal(LeaseCaseStatus.HandedOff, leaseCase.Status);
    }

    [Fact]
    public void CaseHandoffPackage_WithAcknowledgement_Returns_Updated_Package()
    {
        var trackingId = Guid.NewGuid();
        var original = new CaseHandoffPackage(
            Guid.NewGuid(),
            new LeaseCaseId(Guid.NewGuid()),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            null,
            Guid.NewGuid(),
            new[] { new GovernedDocumentId(Guid.NewGuid()) });

        var updated = original.WithAcknowledgement(trackingId);

        Assert.Null(original.DownstreamAcknowledgementId);
        Assert.Equal(trackingId, updated.DownstreamAcknowledgementId);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.LeaseCaseId, updated.LeaseCaseId);
        Assert.Equal(original.FinalDecisionId, updated.FinalDecisionId);
        Assert.Equal(original.FulfilledDocumentIds, updated.FulfilledDocumentIds);
    }
}
