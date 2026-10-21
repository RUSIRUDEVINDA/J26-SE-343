namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.Tracking;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

internal static class LeaseCasePersistenceMapper
{
    private static readonly FieldInfo CounterField = typeof(LeaseCase)
        .GetField("_proposalContentSequenceCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static LeaseCaseEntity ToPersistence(LeaseCase domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var counter = (int)CounterField.GetValue(domain)!;

        return new LeaseCaseEntity
        {
            Id = domain.Id.Value,
            ApplicationReference = domain.ApplicationReference,
            CreatedByActorId = domain.CreatedByActorId,
            CreatedAt = domain.CreatedAt.ToUniversalTime(),
            Revision = domain.Revision,
            Status = domain.Status.ToString(),
            CurrentVerifiedFactSnapshotId = domain.CurrentVerifiedFactSnapshotId,
            ProposalIntakeJson = domain.ProposalIntake is null ? null : PersistenceJsonSerializer.Serialize(domain.ProposalIntake),
            LatestScreeningJson = domain.LatestScreening is null ? null : PersistenceJsonSerializer.Serialize(domain.LatestScreening),
            ActiveWorkflowPlanId = domain.ActiveWorkflowPlan?.Id.Value,
            CompletedWorkflowJson = domain.CompletedWorkflow is null ? null : PersistenceJsonSerializer.Serialize(domain.CompletedWorkflow),
            HandoffPackageJson = domain.HandoffPackage is null ? null : PersistenceJsonSerializer.Serialize(domain.HandoffPackage),
            ApprovalConditionsJson = PersistenceJsonSerializer.Serialize(domain.ApprovalConditions),
            DocumentSubmissionRequirementsJson = PersistenceJsonSerializer.Serialize(domain.DocumentSubmissionRequirements),
            EscalationsJson = PersistenceJsonSerializer.Serialize(domain.Escalations),
            AssessmentHistoryJson = PersistenceJsonSerializer.Serialize(domain.AssessmentHistory),
            ProposalContentAssessmentsJson = PersistenceJsonSerializer.Serialize(domain.ProposalContentAssessmentHistory),
            WorkflowPlanHistoryJson = PersistenceJsonSerializer.Serialize(domain.WorkflowPlanHistory),
            ProposalContentSequenceCounter = counter,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static LeaseCase ToDomain(LeaseCaseEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var domain = AggregateRehydrator.Create<LeaseCase>();

        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.Id), new LeaseCaseId(entity.Id));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.ApplicationReference), entity.ApplicationReference);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.CreatedByActorId), entity.CreatedByActorId);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.CreatedAt), DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.Revision), entity.Revision);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.Status), Enum.Parse<LeaseCaseStatus>(entity.Status));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.CurrentVerifiedFactSnapshotId), entity.CurrentVerifiedFactSnapshotId);

        if (!string.IsNullOrWhiteSpace(entity.ProposalIntakeJson))
        {
            var intake = PersistenceJsonSerializer.Deserialize<LeaseProposalIntake>(entity.ProposalIntakeJson);
            AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.ProposalIntake), intake);
        }

        if (!string.IsNullOrWhiteSpace(entity.LatestScreeningJson))
        {
            var screening = PersistenceJsonSerializer.Deserialize<ScreeningResult>(entity.LatestScreeningJson);
            AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.LatestScreening), screening);
        }

        if (!string.IsNullOrWhiteSpace(entity.CompletedWorkflowJson))
        {
            var completedWorkflow = PersistenceJsonSerializer.Deserialize<CompletedWorkflowDecision>(entity.CompletedWorkflowJson);
            AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.CompletedWorkflow), completedWorkflow);
        }

        if (!string.IsNullOrWhiteSpace(entity.HandoffPackageJson))
        {
            var handoff = PersistenceJsonSerializer.Deserialize<CaseHandoffPackage>(entity.HandoffPackageJson);
            AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.HandoffPackage), handoff);
        }

        var approvalConditions = PersistenceJsonSerializer.Deserialize<List<ApprovalCondition>>(entity.ApprovalConditionsJson)
            ?? new List<ApprovalCondition>();
        AggregateRehydrator.SetField(domain, "_approvalConditions", approvalConditions);

        var docRequirements = PersistenceJsonSerializer.Deserialize<List<DocumentSubmissionRequirement>>(entity.DocumentSubmissionRequirementsJson)
            ?? new List<DocumentSubmissionRequirement>();
        AggregateRehydrator.SetField(domain, "_documentSubmissionRequirements", docRequirements);

        var escalations = PersistenceJsonSerializer.Deserialize<List<EscalationRequest>>(entity.EscalationsJson)
            ?? new List<EscalationRequest>();
        AggregateRehydrator.SetField(domain, "_escalations", escalations);

        var assessments = PersistenceJsonSerializer.Deserialize<List<RequirementAssessmentResult>>(entity.AssessmentHistoryJson)
            ?? new List<RequirementAssessmentResult>();
        AggregateRehydrator.SetField(domain, "_assessmentHistory", assessments);

        var proposalAssessments = PersistenceJsonSerializer.Deserialize<List<ProposalContentCompletenessResult>>(entity.ProposalContentAssessmentsJson)
            ?? new List<ProposalContentCompletenessResult>();
        AggregateRehydrator.SetField(domain, "_proposalContentAssessments", proposalAssessments);

        var planHistory = PersistenceJsonSerializer.Deserialize<List<WorkflowPlan>>(entity.WorkflowPlanHistoryJson)
            ?? new List<WorkflowPlan>();
        AggregateRehydrator.SetField(domain, "_workflowPlanHistory", planHistory);

        if (entity.ActiveWorkflowPlanId.HasValue)
        {
            var activePlan = planHistory.FirstOrDefault(p => p.Id.Value == entity.ActiveWorkflowPlanId.Value);
            AggregateRehydrator.SetPropertyBackingField(domain, nameof(LeaseCase.ActiveWorkflowPlan), activePlan);
        }

        AggregateRehydrator.SetField(domain, "_proposalContentSequenceCounter", entity.ProposalContentSequenceCounter);
        AggregateRehydrator.SetField(domain, "_domainEvents", new List<IDomainEvent>());

        return domain;
    }
}
