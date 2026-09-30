namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public enum ConsensusOutcome
{
    ApprovalRecommended,
    ConditionalApprovalRecommended,
    RejectionRecommended,
    CorrectionsRequired,
    ConsensusNotReached,
    HumanEscalationRequired
}
