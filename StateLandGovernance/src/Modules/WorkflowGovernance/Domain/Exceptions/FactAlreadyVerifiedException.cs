namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class FactAlreadyVerifiedException : WorkflowGovernanceDomainException
{
    public FactAlreadyVerifiedException(string message) : base(message) { }
}
