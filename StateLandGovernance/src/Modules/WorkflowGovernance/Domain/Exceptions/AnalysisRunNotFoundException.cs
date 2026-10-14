namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class AnalysisRunNotFoundException : WorkflowGovernanceDomainException
{
    public AnalysisRunNotFoundException(string message) : base(message) { }
}
