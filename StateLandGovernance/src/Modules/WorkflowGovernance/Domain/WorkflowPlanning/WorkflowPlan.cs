namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Events;

public sealed class WorkflowPlan
{
    public WorkflowPlanId Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public WorkflowPlanSource Source { get; }
    public WorkflowPlanStatus Status { get; private set; }
    public VerifiedFactSnapshotId VerifiedFactSnapshotId { get; }
    public DocumentCompletenessAssessmentId DocumentCompletenessAssessmentId { get; }
    public WorkflowRuleSetReference RuleSetReference { get; }
    public WorkflowRecommendationReference? RecommendationReference { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public string? SupersessionReason { get; private set; }
    public int Revision { get; private set; }

    private readonly List<WorkflowStage> _stages;
    public IReadOnlyCollection<WorkflowStage> Stages => _stages.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public WorkflowPlan(
        WorkflowPlanId id,
        LeaseCaseId leaseCaseId,
        WorkflowPlanSource source,
        VerifiedFactSnapshotId verifiedFactSnapshotId,
        DocumentCompletenessAssessmentId documentCompletenessAssessmentId,
        WorkflowRuleSetReference ruleSetReference,
        WorkflowRecommendationReference? recommendationReference,
        DateTime createdAt,
        IEnumerable<WorkflowStage> stages)
    {
        if (source is WorkflowPlanSource.MachineRecommended or WorkflowPlanSource.Hybrid)
        {
            if (recommendationReference == null)
                throw new InvalidWorkflowPlanException("Machine or Hybrid plans require a recommendation reference.");
        }
        else
        {
            if (recommendationReference != null)
                throw new InvalidWorkflowPlanException("RuleBased and Manual plans must not contain a recommendation reference.");
        }

        if (recommendationReference != null && recommendationReference.GeneratedAt > createdAt)
            throw new InvalidWorkflowPlanException("Recommendation GeneratedAt cannot be later than CreatedAt.");

        Id = id;
        LeaseCaseId = leaseCaseId;
        Source = source;
        VerifiedFactSnapshotId = verifiedFactSnapshotId;
        DocumentCompletenessAssessmentId = documentCompletenessAssessmentId;
        RuleSetReference = ruleSetReference;
        RecommendationReference = recommendationReference;
        CreatedAt = createdAt;
        Status = WorkflowPlanStatus.Draft;
        Revision = 1;

        var stageList = (stages ?? Array.Empty<WorkflowStage>()).ToList();
        if (stageList.Count == 0)
            throw new InvalidWorkflowPlanException("Plan must have at least one stage.");

        if (stageList.Select(s => s.Id).Distinct().Count() != stageList.Count)
            throw new InvalidWorkflowPlanException("Duplicate stage IDs are not allowed.");
        
        if (stageList.Select(s => s.StageCode).Distinct().Count() != stageList.Count)
            throw new InvalidWorkflowPlanException("Duplicate stage codes are not allowed.");

        var stageIds = new HashSet<WorkflowStageId>(stageList.Select(s => s.Id));
        foreach (var s in stageList)
        {
            foreach (var p in s.Prerequisites)
            {
                if (!stageIds.Contains(p))
                    throw new InvalidWorkflowPlanException($"Prerequisite {p.Value} for stage {s.Id.Value} is not in the plan.");
            }
        }

        var finalStages = stageList.Where(s => s.IsFinalDecision).ToList();
        if (finalStages.Count == 0)
            throw new InvalidWorkflowPlanException("Graph must have exactly one final-decision stage.");
        if (finalStages.Count > 1)
            throw new InvalidWorkflowPlanException("Graph cannot have multiple final-decision stages.");
        
        var finalStageId = finalStages[0].Id;
        if (stageList.Any(s => s.Prerequisites.Contains(finalStageId)))
            throw new InvalidWorkflowPlanException("A final stage cannot have dependents.");

        var entryStages = stageList.Where(s => s.Prerequisites.Count == 0).ToList();
        if (entryStages.Count == 0)
            throw new InvalidWorkflowPlanException("Graph must have at least one entry stage.");

        var sorted = TopologicalSort(stageList);
        _stages = sorted;

        var reachableFromEntry = new HashSet<WorkflowStageId>(entryStages.Select(s => s.Id));
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var s in stageList)
            {
                if (!reachableFromEntry.Contains(s.Id) && s.Prerequisites.Any(p => reachableFromEntry.Contains(p)))
                {
                    reachableFromEntry.Add(s.Id);
                    changed = true;
                }
            }
        }

        if (reachableFromEntry.Count != stageList.Count)
            throw new InvalidWorkflowPlanException("Disconnected stages found.");

        foreach (var s in stageList)
        {
            if (s.Id != finalStageId && !CanReach(s.Id, finalStageId, stageList))
                throw new InvalidWorkflowPlanException($"Non-final stage {s.Id.Value} must have a path to the final-decision stage.");
        }

        _domainEvents.Add(new WorkflowPlanCreated(
            Guid.NewGuid(),
            createdAt,
            Id,
            LeaseCaseId,
            Source,
            VerifiedFactSnapshotId,
            DocumentCompletenessAssessmentId,
            _stages.Count,
            Revision
        ));
    }

    private bool CanReach(WorkflowStageId from, WorkflowStageId to, List<WorkflowStage> stages)
    {
        var visited = new HashSet<WorkflowStageId>();
        var queue = new Queue<WorkflowStageId>();
        queue.Enqueue(from);

        var dependentsLookup = stages.ToDictionary(s => s.Id, s => new List<WorkflowStageId>());
        foreach (var s in stages)
        {
            foreach (var p in s.Prerequisites)
            {
                dependentsLookup[p].Add(s.Id);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to) return true;

            if (visited.Add(current))
            {
                foreach (var dep in dependentsLookup[current])
                {
                    queue.Enqueue(dep);
                }
            }
        }
        return false;
    }

    private List<WorkflowStage> TopologicalSort(List<WorkflowStage> stages)
    {
        var inDegree = stages.ToDictionary(s => s.Id, s => s.Prerequisites.Count);
        var dependents = stages.ToDictionary(s => s.Id, s => new List<WorkflowStageId>());
        foreach (var s in stages)
        {
            foreach (var p in s.Prerequisites)
            {
                dependents[p].Add(s.Id);
            }
        }

        var ready = stages
            .Where(s => inDegree[s.Id] == 0)
            .OrderBy(s => s.StageCode.Value)
            .ToList();
            
        var sorted = new List<WorkflowStage>();

        while (ready.Count > 0)
        {
            var node = ready.First();
            ready.RemoveAt(0);
            sorted.Add(node);

            var nextNodes = new List<WorkflowStage>();
            foreach (var childId in dependents[node.Id])
            {
                inDegree[childId]--;
                if (inDegree[childId] == 0)
                {
                    nextNodes.Add(stages.First(s => s.Id == childId));
                }
            }
            
            ready.AddRange(nextNodes);
            ready = ready.OrderBy(s => s.StageCode.Value).ToList();
        }

        if (sorted.Count != stages.Count)
            throw new InvalidWorkflowPlanException("Cycle detected in graph.");

        return sorted;
    }

    public void ApprovePlan(
        Guid approvingActorId,
        DateTime approvedAt,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (Status == WorkflowPlanStatus.Approved)
            throw new WorkflowPlanAlreadyApprovedException("Plan is already approved.");
        if (Status == WorkflowPlanStatus.Superseded)
            throw new InvalidWorkflowPlanStateException("Cannot approve a superseded plan.");

        if (approvingActorId == Guid.Empty)
            throw new InvalidWorkflowPlanException("Approving actor ID cannot be empty.");
            
        if (approvedAt < CreatedAt)
            throw new InvalidWorkflowPlanException("ApprovedAt cannot be before CreatedAt.");

        if (authoritySnapshot == null)
            throw new InvalidWorkflowPlanException("Authority snapshot is required.");

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D"));
        authoritySnapshot.EnsureAuthorizes(approvingActorId, "WorkflowPlanApprover", requiredScope, approvedAt);

        int nextRevision;
        try
        {
            nextRevision = checked(Revision + 1);
        }
        catch (OverflowException)
        {
            throw new WorkflowPlanRevisionOverflowException("Revision limit reached.");
        }

        var ev = new WorkflowPlanApproved(
            Guid.NewGuid(),
            approvedAt,
            Id,
            LeaseCaseId,
            approvingActorId,
            nextRevision
        );

        Status = WorkflowPlanStatus.Approved;
        ApprovedAt = approvedAt;
        Revision = nextRevision;
        _domainEvents.Add(ev);
    }

    public void SupersedePlan(
        Guid supersedingActorId,
        string reason,
        DateTime supersededAt,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (Status == WorkflowPlanStatus.Superseded)
            throw new WorkflowPlanAlreadySupersededException("Plan is already superseded.");

        if (supersedingActorId == Guid.Empty)
            throw new InvalidWorkflowPlanException("Superseding actor ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidWorkflowPlanException("Reason is required to supersede.");
        
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new InvalidWorkflowPlanException("Reason is too long.");
            
        if (trimmedReason.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("Reason cannot contain control characters.");

        if (supersededAt < CreatedAt)
            throw new InvalidWorkflowPlanException("SupersededAt cannot be before CreatedAt.");
            
        if (ApprovedAt.HasValue && supersededAt < ApprovedAt.Value)
            throw new InvalidWorkflowPlanException("SupersededAt cannot be before ApprovedAt.");

        if (authoritySnapshot == null)
            throw new InvalidWorkflowPlanException("Authority snapshot is required.");

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D"));
        authoritySnapshot.EnsureAuthorizes(supersedingActorId, "WorkflowPlanSuperseder", requiredScope, supersededAt);

        int nextRevision;
        try
        {
            nextRevision = checked(Revision + 1);
        }
        catch (OverflowException)
        {
            throw new WorkflowPlanRevisionOverflowException("Revision limit reached.");
        }

        var ev = new WorkflowPlanSuperseded(
            Guid.NewGuid(),
            supersededAt,
            Id,
            LeaseCaseId,
            supersedingActorId,
            trimmedReason,
            nextRevision
        );

        Status = WorkflowPlanStatus.Superseded;
        SupersededAt = supersededAt;
        SupersessionReason = trimmedReason;
        Revision = nextRevision;
        _domainEvents.Add(ev);
    }
    public StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.WorkflowExecutionDefinition CreateExecutionDefinition()
    {
        if (Status != WorkflowPlanStatus.Approved)
            throw new StateLandGovernance.WorkflowGovernance.Domain.Exceptions.InvalidWorkflowPlanStateException("Cannot create execution from a non-Approved plan.");
        if (!ApprovedAt.HasValue)
            throw new StateLandGovernance.WorkflowGovernance.Domain.Exceptions.InvalidWorkflowPlanStateException("Approved plan must have an ApprovedAt timestamp.");

        var stageDefs = _stages.Select(s => new StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.WorkflowStageDefinition(
            s.Id,
            s.StageCode,
            s.InstitutionCode,
            s.StageType,
            s.RequiredOfficerCapability,
            s.RoutingReasonCode,
            s.ReasonDescription,
            s.TargetDurationDays,
            s.IsFinalDecision,
            s.Prerequisites.ToList().AsReadOnly()
        )).ToList().AsReadOnly();

        return new StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.WorkflowExecutionDefinition(
            Id,
            Revision,
            LeaseCaseId,
            RuleSetReference,
            CreatedAt,
            ApprovedAt.Value,
            stageDefs
        );
    }
}




