namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class InvalidVerifiedFactSnapshotException : WorkflowGovernanceDomainException
{
    public InvalidVerifiedFactSnapshotException(string message) : base(message) {}
}
