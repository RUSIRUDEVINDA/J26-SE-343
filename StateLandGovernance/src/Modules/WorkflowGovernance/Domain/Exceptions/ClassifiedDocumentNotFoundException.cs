namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class ClassifiedDocumentNotFoundException : WorkflowGovernanceDomainException
{
    public ClassifiedDocumentNotFoundException(string message) : base(message) { }
}
