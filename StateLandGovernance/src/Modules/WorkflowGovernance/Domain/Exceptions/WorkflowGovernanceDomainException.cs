namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public abstract class WorkflowGovernanceDomainException : Exception
{
    protected WorkflowGovernanceDomainException(string message) : base(message)
    {
    }
}
