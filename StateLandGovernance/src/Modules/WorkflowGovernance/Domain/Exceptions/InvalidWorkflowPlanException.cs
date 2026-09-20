namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class InvalidWorkflowPlanException : WorkflowGovernanceDomainException
{
    public InvalidWorkflowPlanException(string message) : base(message) { }
}
