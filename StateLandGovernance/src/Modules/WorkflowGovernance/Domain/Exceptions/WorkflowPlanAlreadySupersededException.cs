namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class WorkflowPlanAlreadySupersededException : WorkflowGovernanceDomainException
{
    public WorkflowPlanAlreadySupersededException(string message) : base(message) { }
}
