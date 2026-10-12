namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for WorkflowPlan aggregate root.
/// Maps to workflow_governance.workflow_plans.
/// </summary>
public sealed class WorkflowPlanEntity
{
    public Guid Id { get; set; }
    public Guid LeaseCaseId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid VerifiedFactSnapshotId { get; set; }
    public Guid DocumentCompletenessAssessmentId { get; set; }
    public string RuleSetReferenceJson { get; set; } = string.Empty;
    public string? RecommendationReferenceJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public string? SupersessionReason { get; set; }
    public int Revision { get; set; }
    public string StagesJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public LeaseCaseEntity? LeaseCase { get; set; }
}
