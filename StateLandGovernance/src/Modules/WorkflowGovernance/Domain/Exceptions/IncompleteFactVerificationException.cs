namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class IncompleteFactVerificationException : WorkflowGovernanceDomainException
{
    public IncompleteFactVerificationException(string message) : base(message) {}
}
