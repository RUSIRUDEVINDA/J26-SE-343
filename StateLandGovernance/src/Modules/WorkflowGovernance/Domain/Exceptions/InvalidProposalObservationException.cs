namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalObservationException : WorkflowGovernanceDomainException
{
    public InvalidProposalObservationException(string message) : base(message)
    {
    }
}
