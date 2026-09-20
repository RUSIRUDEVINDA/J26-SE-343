using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed record ConsensusParticipantResult
{
    public WorkflowStageId WorkflowStageId { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageDecisionId WorkflowStageDecisionId { get; }
    public WorkflowStageDecisionOutcome WorkflowStageDecisionOutcome { get; }
    public bool IsMandatory { get; }
    public bool IsRejectionBlocking { get; }

    public ConsensusParticipantResult(WorkflowStageId workflowStageId, InstitutionCode institutionCode, WorkflowStageDecisionId workflowStageDecisionId, WorkflowStageDecisionOutcome workflowStageDecisionOutcome, bool isMandatory, bool isRejectionBlocking)
    {
        WorkflowStageId = workflowStageId;
        InstitutionCode = institutionCode;
        WorkflowStageDecisionId = workflowStageDecisionId;
        WorkflowStageDecisionOutcome = workflowStageDecisionOutcome;
        IsMandatory = isMandatory;
        IsRejectionBlocking = isRejectionBlocking;
    }
}
