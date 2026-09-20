namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class DuplicateVerifiedFactSnapshotException : WorkflowGovernanceDomainException
{
    public DuplicateVerifiedFactSnapshotException(string message) : base(message) {}
}
