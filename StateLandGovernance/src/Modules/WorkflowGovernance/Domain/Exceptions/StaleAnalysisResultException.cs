namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class StaleAnalysisResultException : WorkflowGovernanceDomainException
{
    public StaleAnalysisResultException(string message) : base(message) { }
}
