namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class ExtractedFactNotFoundException : WorkflowGovernanceDomainException
{
    public ExtractedFactNotFoundException(string message) : base(message) { }
}
