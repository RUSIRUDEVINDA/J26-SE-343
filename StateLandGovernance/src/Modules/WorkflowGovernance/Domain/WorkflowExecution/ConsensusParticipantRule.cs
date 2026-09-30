using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed record ConsensusParticipantRule
{
    public WorkflowStageId WorkflowStageId { get; }
    public InstitutionCode InstitutionCode { get; }
    public bool IsMandatory { get; }
    public bool IsRejectionBlocking { get; }
    
    public ConsensusParticipantRule(WorkflowStageId workflowStageId, InstitutionCode institutionCode, bool isMandatory, bool isRejectionBlocking)
    {
        if (string.IsNullOrWhiteSpace(institutionCode.Value)) throw new ArgumentException("Institution code is required.");
        
        WorkflowStageId = workflowStageId;
        InstitutionCode = institutionCode;
        IsMandatory = isMandatory;
        IsRejectionBlocking = isRejectionBlocking;
    }
}
