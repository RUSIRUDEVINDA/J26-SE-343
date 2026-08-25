namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowExecution
{
    public WorkflowExecutionId Id { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public int WorkflowPlanRevision { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public WorkflowRuleSetReference RuleSetReference { get; }
    public WorkflowExecutionStatus Status { get; private set; }
    public DateTime StartedAt { get; }
    public int Revision { get; private set; }

    private readonly List<WorkflowStageExecution> _stages;
    public IReadOnlyCollection<WorkflowStageExecution> Stages => _stages.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public WorkflowExecution(WorkflowExecutionId id, WorkflowExecutionDefinition definition, DateTime startedAt)
    {
        if (id.Value == Guid.Empty) throw new InvalidWorkflowExecutionException("Execution ID cannot be empty.");
        if (definition == null) throw new InvalidWorkflowExecutionException("Definition cannot be null.");
        if (startedAt.Kind != DateTimeKind.Utc) throw new InvalidWorkflowExecutionException("StartedAt must be UTC.");
        if (startedAt < definition.PlanApprovedAt) throw new InvalidWorkflowExecutionException("Execution cannot start before the plan is approved.");
        if (!definition.Stages.Any(s => s.IsFinalDecision)) throw new InvalidWorkflowExecutionException("Definition must contain a final decision stage.");

        Id = id;
        WorkflowPlanId = definition.WorkflowPlanId;
        WorkflowPlanRevision = definition.WorkflowPlanRevision;
        LeaseCaseId = definition.LeaseCaseId;
        RuleSetReference = definition.RuleSetReference;
        StartedAt = startedAt;
        Revision = 1;

        _stages = definition.Stages.Select(d => 
        {
            var status = (d.Prerequisites.Count == 0 && !d.IsFinalDecision) ? WorkflowStageExecutionStatus.Ready : WorkflowStageExecutionStatus.Blocked;
            return new WorkflowStageExecution(d, status);
        }).ToList();

        var readyCount = _stages.Count(s => s.Status == WorkflowStageExecutionStatus.Ready);
        var allNonFinalCompleted = !_stages.Any(s => !s.Definition.IsFinalDecision);
        Status = allNonFinalCompleted ? WorkflowExecutionStatus.AwaitingConsensus : WorkflowExecutionStatus.Active;

        _domainEvents.Add(new WorkflowExecutionStarted(
            Guid.NewGuid(), startedAt, Id, WorkflowPlanId, WorkflowPlanRevision, LeaseCaseId, readyCount, Revision));
    }

    internal static int CalculateNextRevision(int currentRevision)
    {
        try
        {
            return checked(currentRevision + 1);
        }
        catch (OverflowException)
        {
            throw new WorkflowExecutionRevisionOverflowException("Revision overflow.");
        }
    }

    public void StartStage(
        WorkflowStageId stageId,
        Guid actingOfficerId,
        DateTime startedAt,
        VerifiedInstitutionalAuthoritySnapshot authoritySnapshot)
    {
        // 1. Validate inputs and locate stage
        if (Status != WorkflowExecutionStatus.Active)
            throw new InvalidWorkflowExecutionException("Execution must be Active to start a stage.");
        if (actingOfficerId == Guid.Empty)
            throw new InvalidWorkflowExecutionException("Actor ID cannot be empty.");
        if (startedAt.Kind != DateTimeKind.Utc)
            throw new InvalidWorkflowExecutionException("StartedAt must be UTC.");
        if (authoritySnapshot == null)
            throw new InvalidWorkflowExecutionException("Authority snapshot is required.");

        var stage = _stages.FirstOrDefault(s => s.Definition.StageId == stageId);
        if (stage == null)
            throw new WorkflowStageExecutionNotFoundException("Stage not found.");

        // 2. Validate state, stage transition and chronology without mutation
        stage.ValidateCanStart(startedAt);
        if (startedAt < StartedAt)
            throw new InvalidWorkflowExecutionException("Stage start time cannot be before execution start time.");

        var prerequisites = _stages.Where(s => stage.Definition.Prerequisites.Contains(s.Definition.StageId)).ToList();
        if (prerequisites.Any() && prerequisites.All(p => p.Decision != null))
        {
            var latestDecidedAt = prerequisites.Max(p => p.Decision!.DecidedAt);
            if (startedAt < latestDecidedAt)
                throw new InvalidWorkflowExecutionException("Stage cannot start before all prerequisite decisions are recorded.");
        }

        // 3. Validate authority
        authoritySnapshot.EnsureAuthorizes(
            actingOfficerId,
            stage.Definition.InstitutionCode,
            stage.Definition.RequiredOfficerCapability,
            new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D")),
            startedAt);

        // 4. Calculate next revision
        var nextRevision = CalculateNextRevision(Revision);

        // 5. Construct event
        var startedEvent = new WorkflowStageStarted(
            Guid.NewGuid(), startedAt, Id, WorkflowPlanId, stage.Definition.StageId, stage.Definition.StageCode,
            stage.Definition.InstitutionCode, actingOfficerId, nextRevision);

        // 6. Mutate stage
        stage.Start(actingOfficerId, startedAt);

        // 7. Assign revision
        Revision = nextRevision;

        // 8. Append event
        _domainEvents.Add(startedEvent);
    }

    public void RecordStageDecision(
        WorkflowStageDecisionId decisionId,
        WorkflowStageId stageId,
        WorkflowStageDecisionOutcome outcome,
        string? reason,
        IReadOnlyCollection<string>? conditions,
        Guid decidingOfficerId,
        DateTime decidedAt,
        VerifiedInstitutionalAuthoritySnapshot authoritySnapshot)
    {
        // 1. Validate inputs and locate stage
        
        if (authoritySnapshot == null)
            throw new InvalidWorkflowExecutionException("Authority snapshot is required.");

        var stage = _stages.FirstOrDefault(s => s.Definition.StageId == stageId);
        if (stage == null)
            throw new WorkflowStageExecutionNotFoundException("Stage not found.");

        // 2. Construct canonical candidate
        var candidate = new WorkflowStageDecision(decisionId, stage.Definition.StageId, stage.Definition.InstitutionCode, outcome, reason, conditions, decidingOfficerId, decidedAt);

        // 3. Apply duplicate/conflict precedence
        var existingAnywhere = _stages.Select(s => s.Decision).FirstOrDefault(d => d != null && d.Id == decisionId);
        if (existingAnywhere != null)
        {
            bool isExact = existingAnywhere.WorkflowStageId == candidate.WorkflowStageId &&
                           existingAnywhere.InstitutionCode.Value == candidate.InstitutionCode.Value &&
                           existingAnywhere.Outcome == candidate.Outcome &&
                           existingAnywhere.Reason == candidate.Reason &&
                           existingAnywhere.DecidingOfficerId == candidate.DecidingOfficerId &&
                           existingAnywhere.DecidedAt == candidate.DecidedAt &&
                           existingAnywhere.Conditions.SequenceEqual(candidate.Conditions);

            if (isExact) throw new DuplicateWorkflowStageDecisionException("Exact decision already exists.");
            throw new ConflictingWorkflowStageDecisionException("Conflicting decision with same ID exists.");
        }

                if (stage.Decision != null)
            throw new WorkflowStageAlreadyDecidedException("Stage is already decided.");

        // 3b. Verify execution is active
        if (Status != WorkflowExecutionStatus.Active)
            throw new InvalidWorkflowExecutionException("Execution must be Active to record a decision.");

        // 4. Validate stage transition and chronology without mutation
        stage.ValidateCanRecordDecision(decidedAt);

        // 5. Validate authority
        authoritySnapshot.EnsureAuthorizes(
            decidingOfficerId,
            stage.Definition.InstitutionCode,
            stage.Definition.RequiredOfficerCapability,
            new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D")),
            decidedAt);

        // 6. Calculate next revision
        var nextRevision = CalculateNextRevision(Revision);

        // 7. Precalculate newly ready stages and next execution status
        var blockedStages = _stages.Where(s => s.Status == WorkflowStageExecutionStatus.Blocked && !s.Definition.IsFinalDecision).ToList();
        var newlyReadyStages = new List<WorkflowStageExecution>();
        
        foreach (var blocked in blockedStages)
        {
            var prereqs = _stages.Where(s => blocked.Definition.Prerequisites.Contains(s.Definition.StageId)).ToList();
            bool allPrereqsMet = true;
            foreach (var p in prereqs)
            {
                if (p.Definition.StageId == stage.Definition.StageId)
                {
                    // Treat the current stage as completed
                    continue;
                }
                if (p.Status != WorkflowStageExecutionStatus.Completed)
                {
                    allPrereqsMet = false;
                    break;
                }
            }
            if (allPrereqsMet)
            {
                newlyReadyStages.Add(blocked);
            }
        }
        var newlyReadyCount = newlyReadyStages.Count;

        bool allNonFinalCompleted = _stages.Where(s => !s.Definition.IsFinalDecision && s.Definition.StageId != stage.Definition.StageId).All(s => s.Status == WorkflowStageExecutionStatus.Completed);
        var nextStatus = allNonFinalCompleted ? WorkflowExecutionStatus.AwaitingConsensus : WorkflowExecutionStatus.Active;

        // 8. Construct event
        var recordedEvent = new WorkflowStageDecisionRecorded(
            Guid.NewGuid(), decidedAt, Id, WorkflowPlanId, stage.Definition.StageId, decisionId,
            stage.Definition.InstitutionCode, outcome, decidingOfficerId, newlyReadyCount, nextStatus, nextRevision);

        // 9. Apply decision and all stage transitions
        stage.RecordDecision(candidate);
        foreach (var nr in newlyReadyStages.OrderBy(s => s.Definition.StageCode.Value, StringComparer.Ordinal))
        {
            nr.Status = WorkflowStageExecutionStatus.Ready;
        }

        // 10. Assign execution status
        Status = nextStatus;

        // 11. Assign revision
        Revision = nextRevision;

        // 12. Append exactly one event
        _domainEvents.Add(recordedEvent);
    }
}

