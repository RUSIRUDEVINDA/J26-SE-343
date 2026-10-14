namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class ConflictingFactVerificationException : WorkflowGovernanceDomainException
{
    public ConflictingFactVerificationException(string message) : base(message) { }
}
