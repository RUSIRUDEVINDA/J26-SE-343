namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class DuplicateFactVerificationException : WorkflowGovernanceDomainException
{
    public DuplicateFactVerificationException(string message) : base(message) { }
}
