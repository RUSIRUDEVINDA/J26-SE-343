namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

public sealed record WorkflowStageDecisionRevision(WorkflowStageDecision PreviousDecision,
    WorkflowStageDecision ReplacementDecision, string Reason, int ExecutionRevision);

public sealed record WorkflowExecutionPlanRevision(WorkflowExecutionDefinition PreviousDefinition,
    WorkflowExecutionDefinition ReplacementDefinition, Guid ActorId, string Reason, DateTime At, int ExecutionRevision);

public sealed partial class WorkflowExecution
{
    private readonly List<WorkflowExecutionPlanRevision> _planHistory = new();
    public IReadOnlyCollection<WorkflowExecutionPlanRevision> PlanHistory => _planHistory.AsReadOnly();
    private readonly List<ConsensusAssessment> _consensusHistory = new();
    public IReadOnlyCollection<ConsensusAssessment> ConsensusHistory => _consensusHistory.AsReadOnly();

    private void InvalidateConsensus()
    {
        if (ConsensusAssessment != null) _consensusHistory.Add(ConsensusAssessment);
        ConsensusAssessment = null;
    }

    private void ValidateRevision(int expectedRevision, string reason, DateTime at)
    {
        if (expectedRevision != Revision) throw new InvalidWorkflowExecutionException("Execution revision is stale.");
        if (Status == WorkflowExecutionStatus.Completed) throw new InvalidWorkflowExecutionException("Completed executions are immutable.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || reason.Any(char.IsControl))
            throw new InvalidWorkflowExecutionException("A revision reason of at most 500 characters is required.");
        var latest = _stages.Select(s => s.Decision?.DecidedAt ?? s.StartedAt ?? StartedAt)
            .Concat(_planHistory.Select(h => h.At)).Max();
        if (at.Kind != DateTimeKind.Utc || at < latest)
            throw new InvalidWorkflowExecutionException("Revision time must be UTC and not precede existing work.");
    }

    public void ReviseStageDecision(WorkflowStageId stageId, int expectedExecutionRevision,
        WorkflowStageDecisionId priorDecisionId, WorkflowStageDecisionId replacementDecisionId,
        WorkflowStageDecisionOutcome outcome, string? decisionReason, IReadOnlyCollection<string>? conditions,
        Guid actorId, VerifiedInstitutionalAuthoritySnapshot authority, string revisionReason, DateTime at)
    {
        ValidateRevision(expectedExecutionRevision, revisionReason, at);
        var stage = _stages.SingleOrDefault(s => s.Definition.StageId == stageId)
            ?? throw new WorkflowStageExecutionNotFoundException("Stage not found.");
        if (stage.Decision == null || stage.Decision.Id != priorDecisionId || stage.Definition.IsFinalDecision)
            throw new InvalidWorkflowExecutionException("Current non-final decision must match the revision request.");
        if (authority == null) throw new InvalidWorkflowExecutionException("Verified authority is required.");
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D"));
        authority.EnsureAuthorizes(actorId, stage.Definition.InstitutionCode, stage.Definition.RequiredOfficerCapability, scope, at);
        authority.EnsureAuthorizes(actorId, stage.Definition.InstitutionCode, "WorkflowDecisionReviser", scope, at);
        if (_stages.Any(s => s.Decision?.Id == replacementDecisionId || s.DecisionHistory.Any(h =>
            h.PreviousDecision.Id == replacementDecisionId || h.ReplacementDecision.Id == replacementDecisionId)))
            throw new InvalidWorkflowExecutionException("Decision IDs cannot be reused.");
        var replacement = new WorkflowStageDecision(replacementDecisionId, stageId, stage.Definition.InstitutionCode,
            outcome, decisionReason, conditions, actorId, at);
        var downstream = Descendants(stageId);
        if (downstream.Any(s => s.StartedAt.HasValue || s.Decision != null))
            throw new InvalidWorkflowExecutionException("Downstream work has started; explicit resolution is required before revision.");
        var next = CalculateNextRevision(Revision);
        stage.ReplaceDecision(replacement, revisionReason.Trim(), next);
        InvalidateConsensus();
        Revision = next;
        RecalculateReadiness();
        _domainEvents.Add(new WorkflowStageDecisionRevised(Guid.NewGuid(), at, Id, stageId,
            priorDecisionId, replacementDecisionId, actorId, revisionReason.Trim(), next));
    }

    public void AdoptRevisedPlan(WorkflowExecutionDefinition approvedDefinition, int expectedExecutionRevision,
        Guid actorId, VerifiedAuthoritySnapshot authority, string reason, DateTime at)
    {
        ValidateRevision(expectedExecutionRevision, reason, at);
        if (approvedDefinition == null || approvedDefinition.LeaseCaseId != LeaseCaseId ||
            approvedDefinition.WorkflowPlanId == WorkflowPlanId || approvedDefinition.PlanApprovedAt > at ||
            approvedDefinition.PlanApprovedAt < Definition.PlanApprovedAt ||
            _planHistory.Any(h => h.PreviousDefinition.WorkflowPlanId == approvedDefinition.WorkflowPlanId ||
                h.ReplacementDefinition.WorkflowPlanId == approvedDefinition.WorkflowPlanId))
            throw new InvalidWorkflowExecutionException("A newer approved plan for the same case is required.");
        if (authority == null) throw new InvalidWorkflowExecutionException("Verified supersession authority is required.");
        authority.EnsureAuthorizes(actorId, "WorkflowPlanSuperseder",
            new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString("D")), at);
        foreach (var old in _stages.Where(s => s.StartedAt.HasValue || s.Decision != null))
        {
            var replacement = approvedDefinition.Stages.SingleOrDefault(s => s.StageId == old.Definition.StageId);
            if (replacement == null || !SameDefinition(old.Definition, replacement) ||
                !SameGate(Definition, approvedDefinition, old.Definition.StageId))
                throw new InvalidWorkflowExecutionException("Started/decided stages and their gates cannot be removed or redefined.");
        }
        var next = CalculateNextRevision(Revision);
        var previousPlanId = WorkflowPlanId;
        var stages = approvedDefinition.Stages.Select(d => _stages.FirstOrDefault(s =>
            s.Definition.StageId == d.StageId && SameDefinition(s.Definition, d)) ??
            new WorkflowStageExecution(d, WorkflowStageExecutionStatus.Blocked)).ToList();
        _planHistory.Add(new WorkflowExecutionPlanRevision(Definition, approvedDefinition, actorId, reason.Trim(), at, next));
        Definition = approvedDefinition;
        WorkflowPlanId = approvedDefinition.WorkflowPlanId;
        WorkflowPlanRevision = approvedDefinition.WorkflowPlanRevision;
        RuleSetReference = approvedDefinition.RuleSetReference;
        _stages.Clear();
        _stages.AddRange(stages);
        InvalidateConsensus();
        Revision = next;
        RecalculateReadiness();
        _domainEvents.Add(new WorkflowExecutionPlanAdopted(Guid.NewGuid(), at, Id, previousPlanId,
            WorkflowPlanId, actorId, reason.Trim(), next));
    }

    private static bool SameGate(WorkflowExecutionDefinition a, WorkflowExecutionDefinition b, WorkflowStageId id) =>
        (a.PrerequisiteGatePolicy?.ApprovalRequiredStages.Contains(id) ?? false) ==
        (b.PrerequisiteGatePolicy?.ApprovalRequiredStages.Contains(id) ?? false);

    private static bool SameDefinition(WorkflowStageDefinition a, WorkflowStageDefinition b) =>
        a.StageId == b.StageId && a.StageCode == b.StageCode && a.InstitutionCode == b.InstitutionCode &&
        a.StageType == b.StageType && a.RequiredOfficerCapability == b.RequiredOfficerCapability &&
        a.RoutingReasonCode == b.RoutingReasonCode && a.ReasonDescription == b.ReasonDescription &&
        a.TargetDurationDays == b.TargetDurationDays && a.IsFinalDecision == b.IsFinalDecision &&
        a.Prerequisites.OrderBy(x => x.Value).SequenceEqual(b.Prerequisites.OrderBy(x => x.Value));

    private List<WorkflowStageExecution> Descendants(WorkflowStageId id)
    {
        var ids = new HashSet<WorkflowStageId> { id };
        bool changed;
        do
        {
            changed = false;
            foreach (var s in _stages)
                if (s.Definition.Prerequisites.Any(ids.Contains)) changed |= ids.Add(s.Definition.StageId);
        } while (changed);
        return _stages.Where(s => s.Definition.StageId != id && ids.Contains(s.Definition.StageId)).ToList();
    }

    private void RecalculateReadiness()
    {
        foreach (var stage in _stages.Where(s => !s.StartedAt.HasValue && s.Decision == null))
        {
            var ready = !stage.Definition.IsFinalDecision && stage.Definition.Prerequisites.All(id =>
                _stages.Any(p => p.Definition.StageId == id && p.Decision != null &&
                    (Definition.PrerequisiteGatePolicy?.Permits(stage.Definition.StageId, p.Decision.Outcome) ?? true)));
            stage.Status = ready ? WorkflowStageExecutionStatus.Ready : WorkflowStageExecutionStatus.Blocked;
        }
        Status = _stages.Where(s => !s.Definition.IsFinalDecision).All(s => s.Decision != null)
            ? WorkflowExecutionStatus.AwaitingConsensus : WorkflowExecutionStatus.Active;
    }
}
