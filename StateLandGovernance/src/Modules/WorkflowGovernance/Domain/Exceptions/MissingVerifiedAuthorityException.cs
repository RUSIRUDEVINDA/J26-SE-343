namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class MissingVerifiedAuthorityException : WorkflowGovernanceDomainException
{
    public MissingVerifiedAuthorityException(string message) : base(message) { }
}
