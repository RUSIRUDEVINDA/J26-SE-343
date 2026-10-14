namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalContentRequirementException : WorkflowGovernanceDomainException
{
    public InvalidProposalContentRequirementException(string message) : base(message)
    {
    }
}
