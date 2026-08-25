using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

public sealed record ConsensusAssessmentRecorded : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public ConsensusAssessmentId ConsensusAssessmentId { get; }
    public ConsensusPolicyId ConsensusPolicyId { get; }
    public ConsensusOutcome ConsensusOutcome { get; }
    public int ParticipantCount { get; }
    public int ApprovalCount { get; }
    public int ConditionalApprovalCount { get; }
    public int RejectionCount { get; }
    public int ChangesRequestedCount { get; }
    public int AbstentionCount { get; }
    public bool BlockingRejectionDetected { get; }
    public int WorkflowExecutionRevision { get; }

    public ConsensusAssessmentRecorded(
        Guid eventId, DateTime occurredOn, WorkflowExecutionId workflowExecutionId, WorkflowPlanId workflowPlanId,
        ConsensusAssessmentId consensusAssessmentId, ConsensusPolicyId consensusPolicyId, ConsensusOutcome consensusOutcome,
        int participantCount, int approvalCount, int conditionalApprovalCount, int rejectionCount,
        int changesRequestedCount, int abstentionCount, bool blockingRejectionDetected, int workflowExecutionRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        ConsensusAssessmentId = consensusAssessmentId;
        ConsensusPolicyId = consensusPolicyId;
        ConsensusOutcome = consensusOutcome;
        ParticipantCount = participantCount;
        ApprovalCount = approvalCount;
        ConditionalApprovalCount = conditionalApprovalCount;
        RejectionCount = rejectionCount;
        ChangesRequestedCount = changesRequestedCount;
        AbstentionCount = abstentionCount;
        BlockingRejectionDetected = blockingRejectionDetected;
        WorkflowExecutionRevision = workflowExecutionRevision;
    }
}
