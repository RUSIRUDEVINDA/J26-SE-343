using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed class WorkflowStageDecision
{
    public WorkflowStageDecisionId Id { get; }
    public WorkflowStageId WorkflowStageId { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageDecisionOutcome Outcome { get; }
    public string? Reason { get; }
    private readonly string[] _conditions;
    public IReadOnlyCollection<string> Conditions => Array.AsReadOnly(_conditions);
    public Guid DecidingOfficerId { get; }
    public DateTime DecidedAt { get; }
    public ConsensusAssessmentId? ConsensusAssessmentId { get; }

    internal WorkflowStageDecision(
        WorkflowStageDecisionId id,
        WorkflowStageId workflowStageId,
        InstitutionCode institutionCode,
        WorkflowStageDecisionOutcome outcome,
        string? reason,
        IEnumerable<string>? conditions,
        Guid decidingOfficerId,
        DateTime decidedAt,
        ConsensusAssessmentId? consensusAssessmentId = null)
    {
        if (id.Value == Guid.Empty) throw new InvalidWorkflowStageDecisionException("Decision ID cannot be empty.");
        if (workflowStageId.Value == Guid.Empty) throw new InvalidWorkflowStageDecisionException("WorkflowStageId cannot be empty.");
        if (institutionCode == default) throw new InvalidWorkflowStageDecisionException("InstitutionCode cannot be default.");
        if (!Enum.IsDefined(typeof(WorkflowStageDecisionOutcome), outcome)) throw new InvalidWorkflowStageDecisionException("Outcome is invalid.");
        if (decidingOfficerId == Guid.Empty) throw new InvalidWorkflowStageDecisionException("DecidingOfficerId cannot be empty.");
        if (decidedAt.Kind != DateTimeKind.Utc) throw new InvalidWorkflowStageDecisionException("DecidedAt must be UTC.");

        var trimmedReason = reason?.Trim();
        if (trimmedReason != null)
        {
            if (trimmedReason.Length > 500) throw new InvalidWorkflowStageDecisionException("Reason cannot exceed 500 characters.");
            if (trimmedReason.Any(char.IsControl)) throw new InvalidWorkflowStageDecisionException("Reason cannot contain control characters.");
        }

        var condList = new List<string>();
        if (conditions != null)
        {
            foreach (var c in conditions)
            {
                if (string.IsNullOrWhiteSpace(c)) throw new InvalidWorkflowStageDecisionException("Condition cannot be null or whitespace.");
                var t = c.Trim();
                if (t.Length > 200) throw new InvalidWorkflowStageDecisionException("Condition cannot exceed 200 characters.");
                if (t.Any(char.IsControl)) throw new InvalidWorkflowStageDecisionException("Condition cannot contain control characters.");
                condList.Add(t);
            }
        }

        var distinctConds = condList.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinctConds.Length != condList.Count) throw new InvalidWorkflowStageDecisionException("Duplicate conditions are not allowed.");
        if (distinctConds.Length > 10) throw new InvalidWorkflowStageDecisionException("Cannot exceed 10 conditions.");
        Array.Sort(distinctConds, StringComparer.OrdinalIgnoreCase);

        switch (outcome)
        {
            case WorkflowStageDecisionOutcome.Approved:
                if (distinctConds.Length > 0) throw new InvalidWorkflowStageDecisionException("Approved outcome must not contain conditions.");
                break;
            case WorkflowStageDecisionOutcome.ApprovedWithConditions:
                if (string.IsNullOrEmpty(trimmedReason)) throw new InvalidWorkflowStageDecisionException("ApprovedWithConditions requires a reason.");
                if (distinctConds.Length == 0) throw new InvalidWorkflowStageDecisionException("ApprovedWithConditions requires at least one condition.");
                break;
            case WorkflowStageDecisionOutcome.Rejected:
                if (string.IsNullOrEmpty(trimmedReason)) throw new InvalidWorkflowStageDecisionException("Rejected requires a reason.");
                if (distinctConds.Length > 0) throw new InvalidWorkflowStageDecisionException("Rejected outcome must not contain conditions.");
                break;
            case WorkflowStageDecisionOutcome.ChangesRequested:
                if (string.IsNullOrEmpty(trimmedReason)) throw new InvalidWorkflowStageDecisionException("ChangesRequested requires a reason.");
                if (distinctConds.Length == 0) throw new InvalidWorkflowStageDecisionException("ChangesRequested requires at least one condition.");
                break;
            case WorkflowStageDecisionOutcome.Abstained:
                if (string.IsNullOrEmpty(trimmedReason)) throw new InvalidWorkflowStageDecisionException("Abstained requires a reason.");
                if (distinctConds.Length > 0) throw new InvalidWorkflowStageDecisionException("Abstained outcome must not contain conditions.");
                break;
        }

        Id = id;
        WorkflowStageId = workflowStageId;
        InstitutionCode = institutionCode;
        Outcome = outcome;
        Reason = trimmedReason;
        _conditions = distinctConds;
        DecidingOfficerId = decidingOfficerId;
        DecidedAt = decidedAt;
        ConsensusAssessmentId = consensusAssessmentId;
    }
}
