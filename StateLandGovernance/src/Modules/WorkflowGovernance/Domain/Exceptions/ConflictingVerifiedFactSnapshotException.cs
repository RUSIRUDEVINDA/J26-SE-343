namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class ConflictingVerifiedFactSnapshotException : WorkflowGovernanceDomainException
{
    public ConflictingVerifiedFactSnapshotException(string message) : base(message) {}
}
