namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct LeaseCaseId
{
    public Guid Value { get; }

    public LeaseCaseId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("LeaseCaseId cannot be empty.");
        }
        Value = value;
    }

    public override string ToString() => Value.ToString();
}
