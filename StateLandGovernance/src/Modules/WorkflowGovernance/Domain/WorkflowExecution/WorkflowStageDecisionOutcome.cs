namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public enum WorkflowStageDecisionOutcome
{
    Approved,
    ApprovedWithConditions,
    Rejected,
    ChangesRequested,
    Abstained
}
