namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct GovernedDocumentId
{
    public Guid Value { get; }
    public GovernedDocumentId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidDocumentException("GovernedDocumentId cannot be empty.");
        Value = value;
    }
    public override string ToString() => Value.ToString();
}
