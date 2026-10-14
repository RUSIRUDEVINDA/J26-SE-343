namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using System;

public sealed class AnalysisRunResultNotFoundException : WorkflowGovernanceDomainException
{
    public AnalysisRunResultNotFoundException(string message) : base(message) { }
}
