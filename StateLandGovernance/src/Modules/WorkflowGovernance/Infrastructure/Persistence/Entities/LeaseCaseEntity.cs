namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// EF Core persistence model for LeaseCase aggregate root.
/// Maps to workflow_governance.lease_cases.
/// </summary>
public sealed class LeaseCaseEntity
{
    public Guid Id { get; set; }
    public string ApplicationReference { get; set; } = string.Empty;
    public Guid CreatedByActorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CurrentVerifiedFactSnapshotId { get; set; }

    public string? ProposalIntakeJson { get; set; }
    public string? LatestScreeningJson { get; set; }
    public Guid? ActiveWorkflowPlanId { get; set; }
    public string? CompletedWorkflowJson { get; set; }
    public string? HandoffPackageJson { get; set; }

    public string ApprovalConditionsJson { get; set; } = "[]";
    public string DocumentSubmissionRequirementsJson { get; set; } = "[]";
    public string EscalationsJson { get; set; } = "[]";
    public string AssessmentHistoryJson { get; set; } = "[]";
    public string ProposalContentAssessmentsJson { get; set; } = "[]";
    public string WorkflowPlanHistoryJson { get; set; } = "[]";
    public int ProposalContentSequenceCounter { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<GovernedDocumentEntity> GovernedDocuments { get; set; } = new List<GovernedDocumentEntity>();
    public ICollection<DocumentCompletenessAssessmentEntity> CompletenessAssessments { get; set; } = new List<DocumentCompletenessAssessmentEntity>();
    public ICollection<WorkflowPlanEntity> WorkflowPlans { get; set; } = new List<WorkflowPlanEntity>();
    public ICollection<WorkflowExecutionEntity> WorkflowExecutions { get; set; } = new List<WorkflowExecutionEntity>();
}
