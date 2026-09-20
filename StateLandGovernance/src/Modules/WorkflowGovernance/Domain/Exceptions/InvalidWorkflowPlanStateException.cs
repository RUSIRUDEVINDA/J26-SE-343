namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class InvalidWorkflowPlanStateException : WorkflowGovernanceDomainException
{
    public InvalidWorkflowPlanStateException(string message) : base(message) { }
}
