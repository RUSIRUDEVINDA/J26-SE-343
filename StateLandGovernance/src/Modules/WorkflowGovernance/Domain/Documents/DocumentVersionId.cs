namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct DocumentVersionId
{
    public Guid Value { get; }
    public DocumentVersionId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidDocumentException("DocumentVersionId cannot be empty.");
        Value = value;
    }
    public override string ToString() => Value.ToString();
}
