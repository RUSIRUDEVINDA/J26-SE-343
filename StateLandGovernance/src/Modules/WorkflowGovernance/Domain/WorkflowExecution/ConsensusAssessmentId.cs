using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public readonly record struct ConsensusAssessmentId
{
    public Guid Value { get; }
    
    public ConsensusAssessmentId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("ConsensusAssessmentId cannot be empty.", nameof(value));
        Value = value;
    }
}
