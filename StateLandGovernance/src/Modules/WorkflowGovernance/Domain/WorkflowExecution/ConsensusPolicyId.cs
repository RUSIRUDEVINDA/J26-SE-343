using System;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public readonly record struct ConsensusPolicyId
{
    public Guid Value { get; }
    
    public ConsensusPolicyId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("ConsensusPolicyId cannot be empty.", nameof(value));
        Value = value;
    }
}
