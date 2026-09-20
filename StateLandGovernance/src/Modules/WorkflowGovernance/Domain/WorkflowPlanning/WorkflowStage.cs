namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class WorkflowStage
{
    public WorkflowStageId Id { get; }
    public WorkflowStageCode StageCode { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageType StageType { get; }
    public string RequiredOfficerCapability { get; }
    public RoutingReasonCode RoutingReasonCode { get; }
    public string ReasonDescription { get; }
    public int? TargetDurationDays { get; }
    public bool IsFinalDecision { get; }

    private readonly List<WorkflowStageId> _prerequisites;
    public IReadOnlyCollection<WorkflowStageId> Prerequisites => _prerequisites.AsReadOnly();

    public WorkflowStage(
        WorkflowStageId id,
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
        if (string.IsNullOrWhiteSpace(requiredOfficerCapability))
            throw new InvalidWorkflowPlanException("RequiredOfficerCapability cannot be empty.");
        var trimmedCap = requiredOfficerCapability.Trim();
        if (trimmedCap.Length > 100)
            throw new InvalidWorkflowPlanException("RequiredOfficerCapability is too long.");
        if (trimmedCap.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("RequiredOfficerCapability cannot contain control characters.");
            
        if (string.IsNullOrWhiteSpace(reasonDescription))
            throw new InvalidWorkflowPlanException("ReasonDescription cannot be empty.");
            
        var trimmedReason = reasonDescription.Trim();
        if (trimmedReason.Length > 500)
            throw new InvalidWorkflowPlanException("ReasonDescription is too long.");
        if (trimmedReason.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("ReasonDescription cannot contain control characters.");

        if (targetDurationDays.HasValue && targetDurationDays.Value <= 0)
            throw new InvalidWorkflowPlanException("TargetDurationDays must be positive.");

        if (isFinalDecision && stageType != WorkflowStageType.FinalDecision)
            throw new InvalidWorkflowPlanException("IsFinalDecision true requires StageType FinalDecision.");
        if (!isFinalDecision && stageType == WorkflowStageType.FinalDecision)
            throw new InvalidWorkflowPlanException("StageType FinalDecision requires IsFinalDecision true.");

        Id = id;
        StageCode = stageCode;
        InstitutionCode = institutionCode;
        StageType = stageType;
        RequiredOfficerCapability = trimmedCap;
        RoutingReasonCode = routingReasonCode;
        ReasonDescription = trimmedReason;
        TargetDurationDays = targetDurationDays;
        IsFinalDecision = isFinalDecision;

        var prereqArray = (prerequisites ?? Array.Empty<WorkflowStageId>()).ToArray();
        
        var distinctPrereqs = prereqArray.Distinct().ToArray();
        if (distinctPrereqs.Length != prereqArray.Length)
            throw new InvalidWorkflowPlanException("Duplicate prerequisites are not allowed.");

        if (distinctPrereqs.Contains(id))
            throw new InvalidWorkflowPlanException("A stage cannot be a prerequisite of itself.");

        _prerequisites = distinctPrereqs.OrderBy(p => p.Value).ToList();
    }
}
