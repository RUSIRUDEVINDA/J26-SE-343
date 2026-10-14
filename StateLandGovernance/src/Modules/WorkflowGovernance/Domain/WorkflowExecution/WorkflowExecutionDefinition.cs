namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowExecutionDefinition
{
    public WorkflowPlanId WorkflowPlanId { get; }
    public int WorkflowPlanRevision { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public WorkflowRuleSetReference RuleSetReference { get; }
    public DateTime PlanCreatedAt { get; }
    public DateTime PlanApprovedAt { get; }
    private readonly WorkflowStageDefinition[] _stages;
    public IReadOnlyCollection<WorkflowStageDefinition> Stages => Array.AsReadOnly(_stages);

    internal WorkflowExecutionDefinition(
        WorkflowPlanId workflowPlanId,
        int workflowPlanRevision,
        LeaseCaseId leaseCaseId,
        WorkflowRuleSetReference ruleSetReference,
        DateTime planCreatedAt,
        DateTime planApprovedAt,
        IEnumerable<WorkflowStageDefinition> stages)
    {
        if (workflowPlanId.Value == Guid.Empty) throw new InvalidWorkflowExecutionException("Plan ID cannot be empty.");
        if (workflowPlanRevision <= 0) throw new InvalidWorkflowExecutionException("Plan revision must be positive.");
        if (leaseCaseId.Value == Guid.Empty) throw new InvalidWorkflowExecutionException("LeaseCase ID cannot be empty.");
        if (ruleSetReference.Identifier == null || string.IsNullOrWhiteSpace(ruleSetReference.Identifier)) throw new InvalidWorkflowExecutionException("Rule set reference cannot be default.");
        if (planCreatedAt.Kind != DateTimeKind.Utc || planApprovedAt.Kind != DateTimeKind.Utc) throw new InvalidWorkflowExecutionException("Timestamps must be UTC.");
        if (planApprovedAt < planCreatedAt) throw new InvalidWorkflowExecutionException("PlanApprovedAt cannot be before PlanCreatedAt.");
        
        var stagesArr = stages?.ToArray();
        if (stagesArr == null || stagesArr.Length == 0) throw new InvalidWorkflowExecutionException("Stages cannot be null or empty.");
        if (stagesArr.Any(s => s == null)) throw new InvalidWorkflowExecutionException("Stage elements cannot be null.");
        
        var finalStageCount = stagesArr.Count(s => s.IsFinalDecision);
        if (finalStageCount == 0) throw new InvalidWorkflowExecutionException("Definition must contain a final decision stage.");
        if (finalStageCount > 1) throw new InvalidWorkflowExecutionException("Definition cannot contain more than one final decision stage.");

        WorkflowPlanId = workflowPlanId;
        WorkflowPlanRevision = workflowPlanRevision;
        LeaseCaseId = leaseCaseId;
        RuleSetReference = ruleSetReference;
        PlanCreatedAt = planCreatedAt;
        PlanApprovedAt = planApprovedAt;
        _stages = stagesArr;
    }
}

public sealed class WorkflowStageDefinition
{
    public WorkflowStageId StageId { get; }
    public WorkflowStageCode StageCode { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageType StageType { get; }
    public string RequiredOfficerCapability { get; }
    public RoutingReasonCode RoutingReasonCode { get; }
    public string ReasonDescription { get; }
    public int? TargetDurationDays { get; }
    public bool IsFinalDecision { get; }
    private readonly WorkflowStageId[] _prerequisites;
    public IReadOnlyCollection<WorkflowStageId> Prerequisites => Array.AsReadOnly(_prerequisites);

    internal WorkflowStageDefinition(
        WorkflowStageId stageId,
        WorkflowStageCode stageCode,
        InstitutionCode institutionCode,
        WorkflowStageType stageType,
        string requiredOfficerCapability,
        RoutingReasonCode routingReasonCode,
        string reasonDescription,
        int? targetDurationDays,
        bool isFinalDecision,
        IEnumerable<WorkflowStageId> prerequisites)
    {
        if (stageId.Value == Guid.Empty) throw new InvalidWorkflowExecutionException("Stage ID cannot be empty.");
        if (string.IsNullOrWhiteSpace(stageCode.Value)) throw new InvalidWorkflowExecutionException("StageCode cannot be empty.");
        if (institutionCode == default) throw new InvalidWorkflowExecutionException("InstitutionCode cannot be default.");
        if (!Enum.IsDefined(typeof(WorkflowStageType), stageType)) throw new InvalidWorkflowExecutionException("StageType is invalid.");
        
        var trimmedCap = requiredOfficerCapability?.Trim();
        if (string.IsNullOrEmpty(trimmedCap)) throw new InvalidWorkflowExecutionException("Capability cannot be empty.");
        if (trimmedCap.Length > 100) throw new InvalidWorkflowExecutionException("Capability cannot exceed 100 characters.");
        if (trimmedCap.Any(char.IsControl)) throw new InvalidWorkflowExecutionException("Capability cannot contain control characters.");
        
        if (string.IsNullOrWhiteSpace(routingReasonCode.Value)) throw new InvalidWorkflowExecutionException("RoutingReasonCode cannot be default.");
        
        if (string.IsNullOrWhiteSpace(reasonDescription)) throw new InvalidWorkflowExecutionException("ReasonDescription cannot be empty.");
        var trimmedReason = reasonDescription.Trim();
        if (trimmedReason.Length > 500) throw new InvalidWorkflowExecutionException("ReasonDescription cannot exceed 500 characters.");
        if (trimmedReason.Any(char.IsControl)) throw new InvalidWorkflowExecutionException("ReasonDescription cannot contain control characters.");
        
        if (targetDurationDays.HasValue && targetDurationDays.Value <= 0) throw new InvalidWorkflowExecutionException("TargetDurationDays must be positive.");
        
        if (isFinalDecision != (stageType == WorkflowStageType.FinalDecision)) throw new InvalidWorkflowExecutionException("IsFinalDecision must exactly match WorkflowStageType.FinalDecision.");
        
        var prereqArray = prerequisites?.ToArray() ?? Array.Empty<WorkflowStageId>();
        if (prereqArray.Any(p => p.Value == Guid.Empty)) throw new InvalidWorkflowExecutionException("Prerequisite ID cannot be empty.");
        if (prereqArray.Distinct().Count() != prereqArray.Length) throw new InvalidWorkflowExecutionException("Prerequisite IDs cannot contain duplicates.");
        
        requiredOfficerCapability = trimmedCap;
        reasonDescription = trimmedReason;

        StageId = stageId;
        StageCode = stageCode;
        InstitutionCode = institutionCode;
        StageType = stageType;
        RequiredOfficerCapability = requiredOfficerCapability;
        RoutingReasonCode = routingReasonCode;
        ReasonDescription = reasonDescription;
        TargetDurationDays = targetDurationDays;
        IsFinalDecision = isFinalDecision;
        _prerequisites = prerequisites?.ToArray() ?? Array.Empty<WorkflowStageId>();
    }
}




