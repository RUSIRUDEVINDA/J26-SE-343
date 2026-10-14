namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidAuthoritySnapshotException : WorkflowGovernanceDomainException
{
    public InvalidAuthoritySnapshotException(string message) : base(message) { }
}
