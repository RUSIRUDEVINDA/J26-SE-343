namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public enum WorkflowExecutionStatus
{
    Active,
    AwaitingConsensus,
    ReadyForFinalDecision,
    Completed
}
