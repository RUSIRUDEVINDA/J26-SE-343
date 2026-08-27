namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record DocumentContentReference
{
    public string Value { get; }

    public DocumentContentReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidContentReferenceException("Content reference cannot be null or blank.");
        Value = value;
    }
}
