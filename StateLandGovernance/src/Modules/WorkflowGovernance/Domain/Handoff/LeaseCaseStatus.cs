namespace StateLandGovernance.WorkflowGovernance.Domain.Handoff;

public enum LeaseCaseStatus
{
    Draft,
    InWorkflow,
    ApprovedWithConditions,
    ReadyForHandoff,
    HandedOff
}
