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
        if (actingOfficerId == Guid.Empty)
            throw new InvalidWorkflowExecutionException("Actor ID cannot be empty.");
        if (startedAt.Kind != DateTimeKind.Utc)
            throw new InvalidWorkflowExecutionException("StartedAt must be UTC.");
        if (authoritySnapshot == null)
            throw new InvalidWorkflowExecutionException("Authority snapshot is required.");

        var stage = _stages.FirstOrDefault(s => s.Definition.StageId == stageId);
        if (stage == null)
            throw new WorkflowStageExecutionNotFoundException("Stage not found.");

        if (stage.Definition.IsFinalDecision)
        {
            if (Status != WorkflowExecutionStatus.ReadyForFinalDecision)
                throw new InvalidWorkflowStageTransitionException("Execution must be ReadyForFinalDecision to start the final stage.");
        }
        else
        {
            if (Status != WorkflowExecutionStatus.Active)
                throw new InvalidWorkflowExecutionException("Execution must be Active to start a stage.");
        }

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

        if (stage.Definition.IsFinalDecision)
        {
            if (outcome == WorkflowStageDecisionOutcome.ChangesRequested)
                throw new InvalidWorkflowStageTransitionException("Final decision cannot be ChangesRequested.");
            if (outcome == WorkflowStageDecisionOutcome.Abstained)
                throw new InvalidWorkflowStageTransitionException("Final decision cannot be Abstained.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidWorkflowStageTransitionException("Final decision requires a reason.");
        }

        // 2. Construct canonical candidate
        var candidateAssessmentId = stage.Definition.IsFinalDecision && ConsensusAssessment != null
            ? ConsensusAssessment.AssessmentId
            : (ConsensusAssessmentId?)null;
        var candidate = new WorkflowStageDecision(decisionId, stage.Definition.StageId, stage.Definition.InstitutionCode, outcome, reason, conditions, decidingOfficerId, decidedAt, candidateAssessmentId);

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
        if (stage.Definition.IsFinalDecision)
        {
            if (Status != WorkflowExecutionStatus.ReadyForFinalDecision)
                throw new InvalidWorkflowStageTransitionException("Execution must be ReadyForFinalDecision to record a final decision.");
        }
        else
        {
            if (Status != WorkflowExecutionStatus.Active)
                throw new InvalidWorkflowExecutionException("Execution must be Active to record a decision.");
        }

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
        if (stage.Definition.IsFinalDecision) nextStatus = WorkflowExecutionStatus.Completed;

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

        // 12. Append events
        _domainEvents.Add(recordedEvent);
        if (nextStatus == WorkflowExecutionStatus.Completed)
        {
            _domainEvents.Add(new WorkflowExecutionCompleted(
                Guid.NewGuid(), decidedAt, Id, WorkflowPlanId, ConsensusAssessment!.AssessmentId,
                stage.Definition.StageId, decisionId, stage.Definition.InstitutionCode, outcome, decidingOfficerId, nextRevision));
        }
    }

    public ConsensusAssessment? ConsensusAssessment { get; private set; }

    public void EvaluateConsensus(
        ConsensusAssessmentId assessmentId,
        ConsensusPolicySnapshot policy,
        DateTime evaluatedAt)
    {
        if (assessmentId.Value == Guid.Empty) throw new InvalidConsensusAssessmentException("Assessment ID cannot be empty.");
        if (policy == null) throw new InvalidConsensusPolicyException("Policy cannot be null.");
        if (evaluatedAt.Kind != DateTimeKind.Utc) throw new InvalidConsensusAssessmentException("EvaluatedAt must be UTC.");

        if (ConsensusAssessment != null)
        {
            if (ConsensusAssessment.AssessmentId.Value == assessmentId.Value)
            {
                bool isExact = CompareCanonicalAssessments(ConsensusAssessment, policy, evaluatedAt);
                if (isExact) throw new DuplicateConsensusAssessmentException("Exact consensus assessment already exists.");
                throw new ConflictingConsensusAssessmentException("Conflicting consensus assessment with same ID exists.");
            }
            throw new ConsensusAlreadyEvaluatedException("Execution already has a consensus assessment.");
        }

        if (Status != WorkflowExecutionStatus.AwaitingConsensus)
            throw new InvalidWorkflowExecutionException("Execution must be AwaitingConsensus to evaluate consensus.");

        if (policy.GeneratedAt.Kind != DateTimeKind.Utc) throw new InvalidConsensusPolicyException("Policy GeneratedAt must be UTC.");
        if (policy.GeneratedAt > StartedAt) throw new InvalidConsensusPolicyException("Policy GeneratedAt must not be after execution StartedAt.");
        if (evaluatedAt < policy.GeneratedAt) throw new InvalidConsensusAssessmentException("EvaluatedAt cannot be before policy GeneratedAt.");

        if (policy.LeaseCaseId.Value != LeaseCaseId.Value ||
            policy.WorkflowPlanId.Value != WorkflowPlanId.Value ||
            policy.WorkflowPlanRevision != WorkflowPlanRevision ||
            policy.RuleSetReference.Identifier != RuleSetReference.Identifier ||
            policy.RuleSetReference.Version != RuleSetReference.Version)
        {
            throw new ConsensusPolicyMismatchException("Policy bindings do not match workflow execution exactly.");
        }

        var nonFinalStages = _stages.Where(s => !s.Definition.IsFinalDecision).ToList();

        if (policy.Participants.Any(p => _stages.FirstOrDefault(s => s.Definition.StageId.Value == p.WorkflowStageId.Value)?.Definition.IsFinalDecision == true))
            throw new ConsensusParticipantNotFoundException("FinalDecision stage cannot be a policy participant.");

        if (policy.Participants.Count != nonFinalStages.Count)
            throw new ConsensusParticipantNotFoundException("Policy participants count does not match non-final execution stages count.");

        foreach (var nonFinal in nonFinalStages)
        {
            var p = policy.Participants.FirstOrDefault(x => x.WorkflowStageId.Value == nonFinal.Definition.StageId.Value);
            if (p == null) throw new ConsensusParticipantNotFoundException("Missing participant in policy.");
            if (p.InstitutionCode.Value != nonFinal.Definition.InstitutionCode.Value)
                throw new ConsensusParticipantNotFoundException("InstitutionCode mismatch in participant.");
        }

        if (nonFinalStages.Any(s => s.Status != WorkflowStageExecutionStatus.Completed || s.Decision == null))
            throw new InvalidConsensusAssessmentException("All non-final stages must be completed with a decision.");

        if (nonFinalStages.Any(s => evaluatedAt < s.Decision!.DecidedAt))
            throw new InvalidConsensusAssessmentException("EvaluatedAt cannot be before any participating decision DecidedAt.");

        var participantResults = new System.Collections.Generic.List<ConsensusParticipantResult>();
        int approvalCount = 0;
        int conditionalCount = 0;
        int rejectionCount = 0;
        int changesRequestedCount = 0;
        int abstentionCount = 0;
        bool blockingRejection = false;
        bool changesRequestedDetected = false;
        bool mandatoryAbstention = false;

        foreach (var p in policy.Participants.OrderBy(x => x.WorkflowStageId.Value))
        {
            var stage = nonFinalStages.First(s => s.Definition.StageId.Value == p.WorkflowStageId.Value);
            var decision = stage.Decision!;
            var result = new ConsensusParticipantResult(p.WorkflowStageId, p.InstitutionCode, decision.Id, decision.Outcome, p.IsMandatory, p.IsRejectionBlocking);
            participantResults.Add(result);

            switch (decision.Outcome)
            {
                case WorkflowStageDecisionOutcome.Approved:
                    approvalCount++;
                    break;
                case WorkflowStageDecisionOutcome.ApprovedWithConditions:
                    approvalCount++;
                    conditionalCount++;
                    break;
                case WorkflowStageDecisionOutcome.Rejected:
                    rejectionCount++;
                    if (p.IsRejectionBlocking) blockingRejection = true;
                    break;
                case WorkflowStageDecisionOutcome.ChangesRequested:
                    changesRequestedCount++;
                    changesRequestedDetected = true;
                    break;
                case WorkflowStageDecisionOutcome.Abstained:
                    abstentionCount++;
                    if (p.IsMandatory) mandatoryAbstention = true;
                    break;
            }
        }

        ConsensusOutcome outcome;
        var reasons = new System.Collections.Generic.List<ConsensusReasonCode>();

        if (blockingRejection)
        {
            outcome = ConsensusOutcome.RejectionRecommended;
            reasons.Add(new ConsensusReasonCode("BlockingRejectionDetected"));
        }
        else if (changesRequestedDetected)
        {
            outcome = ConsensusOutcome.CorrectionsRequired;
            reasons.Add(new ConsensusReasonCode("ChangesRequestedDetected"));
        }
        else if (mandatoryAbstention)
        {
            outcome = ConsensusOutcome.HumanEscalationRequired;
            reasons.Add(new ConsensusReasonCode("MandatoryAbstentionDetected"));
        }
        else if (rejectionCount == participantResults.Count)
        {
            outcome = ConsensusOutcome.RejectionRecommended;
            reasons.Add(new ConsensusReasonCode("AllParticipantsRejected"));
        }
        else
        {
            bool rulePassed = false;
            if (policy.RuleType == ConsensusRuleType.Unanimous)
            {
                rulePassed = (approvalCount == participantResults.Count);
                if (!rulePassed) reasons.Add(new ConsensusReasonCode("UnanimousFailed"));
            }
            else
            {
                rulePassed = (approvalCount >= policy.RequiredApprovalCount!.Value);
                if (!rulePassed) reasons.Add(new ConsensusReasonCode("ThresholdNotMet"));
            }

            if (rulePassed)
            {
                if (conditionalCount > 0)
                {
                    outcome = ConsensusOutcome.ConditionalApprovalRecommended;
                    reasons.Add(new ConsensusReasonCode("RulePassedWithConditions"));
                }
                else
                {
                    outcome = ConsensusOutcome.ApprovalRecommended;
                    reasons.Add(new ConsensusReasonCode("RulePassedCleanly"));
                }
            }
            else
            {
                outcome = ConsensusOutcome.ConsensusNotReached;
            }
        }

        var candidate = new ConsensusAssessment(
            assessmentId, Id, WorkflowPlanId, WorkflowPlanRevision, LeaseCaseId, RuleSetReference,
            policy.PolicyId, policy.PolicyIdentifier, policy.PolicyVersion, policy.GeneratedAt, policy.RuleType, policy.RequiredApprovalCount,
            evaluatedAt, outcome, participantResults.Count, approvalCount, conditionalCount, rejectionCount, changesRequestedCount,
            abstentionCount, blockingRejection, reasons, participantResults);

        var nextRevision = CalculateNextRevision(Revision);

        var recordedEvent = new ConsensusAssessmentRecorded(
            Guid.NewGuid(), evaluatedAt, Id, WorkflowPlanId, assessmentId, policy.PolicyId, outcome,
            participantResults.Count, approvalCount, conditionalCount, rejectionCount, changesRequestedCount, abstentionCount,
            blockingRejection, nextRevision);

        ConsensusAssessment = candidate;
        Status = WorkflowExecutionStatus.ReadyForFinalDecision;

        var finalStage = _stages.Single(s => s.Definition.IsFinalDecision);
        finalStage.Status = WorkflowStageExecutionStatus.Ready;

        Revision = nextRevision;
        _domainEvents.Add(recordedEvent);
    }

    private bool CompareCanonicalAssessments(ConsensusAssessment a, ConsensusPolicySnapshot policy, DateTime evaluatedAt)
    {
        if (a.PolicyId.Value != policy.PolicyId.Value) return false;
        if (a.PolicyIdentifier != policy.PolicyIdentifier) return false;
        if (a.PolicyVersion != policy.PolicyVersion) return false;
        if (a.PolicyGeneratedAt != policy.GeneratedAt) return false;
        if (a.LeaseCaseId.Value != policy.LeaseCaseId.Value) return false;
        if (a.WorkflowPlanId.Value != policy.WorkflowPlanId.Value) return false;
        if (a.WorkflowPlanRevision != policy.WorkflowPlanRevision) return false;
        if (a.RuleSetReference.Identifier != policy.RuleSetReference.Identifier || a.RuleSetReference.Version != policy.RuleSetReference.Version) return false;
        if (a.RuleType != policy.RuleType) return false;
        if (a.RequiredApprovalCount != policy.RequiredApprovalCount) return false;
        if (a.EvaluatedAt != evaluatedAt) return false;

        var nonFinalStages = _stages.Where(s => !s.Definition.IsFinalDecision).ToList();
        if (policy.Participants.Count != nonFinalStages.Count) return false;

        int approvalCount = 0;
        int conditionalCount = 0;
        int rejectionCount = 0;
        int changesRequestedCount = 0;
        int abstentionCount = 0;
        bool blockingRejection = false;
        bool changesRequestedDetected = false;
        bool mandatoryAbstention = false;

        var prA = a.ParticipantResults.OrderBy(x => x.WorkflowStageId.Value).ToList();
        var prB = new System.Collections.Generic.List<ConsensusParticipantResult>();

        foreach (var p in policy.Participants.OrderBy(x => x.WorkflowStageId.Value))
        {
            var stage = nonFinalStages.FirstOrDefault(s => s.Definition.StageId.Value == p.WorkflowStageId.Value);
            if (stage == null || stage.Decision == null) return false;
            if (p.InstitutionCode.Value != stage.Definition.InstitutionCode.Value) return false;

            var decision = stage.Decision;
            var result = new ConsensusParticipantResult(p.WorkflowStageId, p.InstitutionCode, decision.Id, decision.Outcome, p.IsMandatory, p.IsRejectionBlocking);
            prB.Add(result);

            switch (decision.Outcome)
            {
                case WorkflowStageDecisionOutcome.Approved:
                    approvalCount++;
                    break;
                case WorkflowStageDecisionOutcome.ApprovedWithConditions:
                    approvalCount++;
                    conditionalCount++;
                    break;
                case WorkflowStageDecisionOutcome.Rejected:
                    rejectionCount++;
                    if (p.IsRejectionBlocking) blockingRejection = true;
                    break;
                case WorkflowStageDecisionOutcome.ChangesRequested:
                    changesRequestedCount++;
                    changesRequestedDetected = true;
                    break;
                case WorkflowStageDecisionOutcome.Abstained:
                    abstentionCount++;
                    if (p.IsMandatory) mandatoryAbstention = true;
                    break;
            }
        }

        if (prA.Count != prB.Count) return false;
        for (int i = 0; i < prA.Count; i++)
        {
            var pA = prA[i];
            var pB = prB[i];
            if (pA.WorkflowStageId.Value != pB.WorkflowStageId.Value) return false;
            if (pA.InstitutionCode.Value != pB.InstitutionCode.Value) return false;
            if (pA.WorkflowStageDecisionId.Value != pB.WorkflowStageDecisionId.Value) return false;
            if (pA.WorkflowStageDecisionOutcome != pB.WorkflowStageDecisionOutcome) return false;
            if (pA.IsMandatory != pB.IsMandatory) return false;
            if (pA.IsRejectionBlocking != pB.IsRejectionBlocking) return false;
        }

        if (a.ApprovalCount != approvalCount) return false;
        if (a.ConditionalApprovalCount != conditionalCount) return false;
        if (a.RejectionCount != rejectionCount) return false;
        if (a.ChangesRequestedCount != changesRequestedCount) return false;
        if (a.AbstentionCount != abstentionCount) return false;
        if (a.BlockingRejectionDetected != blockingRejection) return false;

        ConsensusOutcome outcome;
        var reasons = new System.Collections.Generic.List<ConsensusReasonCode>();

        if (blockingRejection)
        {
            outcome = ConsensusOutcome.RejectionRecommended;
            reasons.Add(new ConsensusReasonCode("BlockingRejectionDetected"));
        }
        else if (changesRequestedDetected)
        {
            outcome = ConsensusOutcome.CorrectionsRequired;
            reasons.Add(new ConsensusReasonCode("ChangesRequestedDetected"));
        }
        else if (mandatoryAbstention)
        {
            outcome = ConsensusOutcome.HumanEscalationRequired;
            reasons.Add(new ConsensusReasonCode("MandatoryAbstentionDetected"));
        }
        else if (rejectionCount == prB.Count)
        {
            outcome = ConsensusOutcome.RejectionRecommended;
            reasons.Add(new ConsensusReasonCode("AllParticipantsRejected"));
        }
        else
        {
            bool rulePassed = false;
            if (policy.RuleType == ConsensusRuleType.Unanimous)
            {
                rulePassed = (approvalCount == prB.Count);
                if (!rulePassed) reasons.Add(new ConsensusReasonCode("UnanimousFailed"));
            }
            else
            {
                rulePassed = (approvalCount >= policy.RequiredApprovalCount!.Value);
                if (!rulePassed) reasons.Add(new ConsensusReasonCode("ThresholdNotMet"));
            }

            if (rulePassed)
            {
                if (conditionalCount > 0)
                {
                    outcome = ConsensusOutcome.ConditionalApprovalRecommended;
                    reasons.Add(new ConsensusReasonCode("RulePassedWithConditions"));
                }
                else
                {
                    outcome = ConsensusOutcome.ApprovalRecommended;
                    reasons.Add(new ConsensusReasonCode("RulePassedCleanly"));
                }
            }
            else
            {
                outcome = ConsensusOutcome.ConsensusNotReached;
            }
        }

        if (a.Outcome != outcome) return false;

        var reasonsA = a.ReasonCodes.Select(r => r.Value).OrderBy(x => x).ToList();
        var reasonsB = reasons.Select(r => r.Value).OrderBy(x => x).ToList();
        if (!System.Linq.Enumerable.SequenceEqual(reasonsA, reasonsB)) return false;

        return true;
    }
}

