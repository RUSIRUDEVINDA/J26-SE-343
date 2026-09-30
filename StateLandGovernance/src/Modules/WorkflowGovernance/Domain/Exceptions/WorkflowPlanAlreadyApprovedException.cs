namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class WorkflowPlanAlreadyApprovedException : WorkflowGovernanceDomainException
{
    public WorkflowPlanAlreadyApprovedException(string message) : base(message) { }
}
