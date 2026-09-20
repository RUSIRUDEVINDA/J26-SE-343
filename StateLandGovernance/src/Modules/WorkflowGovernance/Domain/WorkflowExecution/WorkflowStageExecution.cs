namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class WorkflowStageExecution
{
    public WorkflowStageDefinition Definition { get; }
    public WorkflowStageExecutionStatus Status { get; internal set; }
    public DateTime? StartedAt { get; internal set; }
    public Guid? ActingOfficerId { get; internal set; }
    public WorkflowStageDecision? Decision { get; internal set; }

    internal WorkflowStageExecution(WorkflowStageDefinition definition, WorkflowStageExecutionStatus initialStatus)
    {
        Definition = definition;
        Status = initialStatus;
    }

    internal void ValidateCanStart(DateTime startedAt)
    {
        if (Status != WorkflowStageExecutionStatus.Ready)
            throw new InvalidWorkflowStageTransitionException("Only Ready stages can be started.");
    }

    internal void Start(Guid actingOfficerId, DateTime startedAt)
    {
        ValidateCanStart(startedAt);
            
        Status = WorkflowStageExecutionStatus.InProgress;
        ActingOfficerId = actingOfficerId;
        StartedAt = startedAt;
    }

    internal void ValidateCanRecordDecision(DateTime decidedAt)
    {
        if (Status != WorkflowStageExecutionStatus.InProgress)
            throw new InvalidWorkflowStageTransitionException("Decisions can only be recorded for InProgress stages.");

        if (decidedAt < StartedAt!.Value)
            throw new InvalidWorkflowStageTransitionException("Decision time cannot be before stage start time.");
            
        if (Decision != null)
            throw new WorkflowStageAlreadyDecidedException("Stage is already decided.");
    }

    internal void RecordDecision(WorkflowStageDecision decision)
    {
        ValidateCanRecordDecision(decision.DecidedAt);

        Decision = decision;
        Status = WorkflowStageExecutionStatus.Completed;
    }
}
