namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

using System;

public sealed class VerifiedFactSnapshotAlreadyPublishedException : WorkflowGovernanceDomainException
{
    public VerifiedFactSnapshotAlreadyPublishedException(string message) : base(message) {}
}
