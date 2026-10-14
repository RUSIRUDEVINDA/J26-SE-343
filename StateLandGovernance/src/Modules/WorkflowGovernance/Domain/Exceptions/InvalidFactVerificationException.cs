namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class InvalidFactVerificationException : WorkflowGovernanceDomainException
{
    public InvalidFactVerificationException(string message) : base(message) { }
}
