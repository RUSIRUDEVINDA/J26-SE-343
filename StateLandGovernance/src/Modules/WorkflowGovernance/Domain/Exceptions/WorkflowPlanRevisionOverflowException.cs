namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class WorkflowPlanRevisionOverflowException : WorkflowGovernanceDomainException
{
    public WorkflowPlanRevisionOverflowException(string message) : base(message) { }
}
