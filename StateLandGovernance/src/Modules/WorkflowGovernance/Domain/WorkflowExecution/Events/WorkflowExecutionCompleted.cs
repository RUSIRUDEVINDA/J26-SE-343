using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

public sealed record WorkflowExecutionCompleted : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public ConsensusAssessmentId ConsensusAssessmentId { get; }
    public WorkflowStageId FinalWorkflowStageId { get; }
    public WorkflowStageDecisionId WorkflowStageDecisionId { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageDecisionOutcome FinalOutcome { get; }
    public Guid DecidingOfficerId { get; }
    public int WorkflowExecutionRevision { get; }

    public WorkflowExecutionCompleted(
        Guid eventId, DateTime occurredOn, WorkflowExecutionId workflowExecutionId, WorkflowPlanId workflowPlanId,
        ConsensusAssessmentId consensusAssessmentId, WorkflowStageId finalWorkflowStageId, WorkflowStageDecisionId workflowStageDecisionId,
        InstitutionCode institutionCode, WorkflowStageDecisionOutcome finalOutcome, Guid decidingOfficerId, int workflowExecutionRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        ConsensusAssessmentId = consensusAssessmentId;
        FinalWorkflowStageId = finalWorkflowStageId;
        WorkflowStageDecisionId = workflowStageDecisionId;
        InstitutionCode = institutionCode;
        FinalOutcome = finalOutcome;
        DecidingOfficerId = decidingOfficerId;
        WorkflowExecutionRevision = workflowExecutionRevision;
    }
}
