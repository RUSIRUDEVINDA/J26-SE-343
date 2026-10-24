namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class VerifiedFactSnapshotNotFoundException : Exception
{
    public Guid SnapshotId { get; }

    public VerifiedFactSnapshotNotFoundException(Guid snapshotId)
        : base($"VerifiedFactSnapshot '{snapshotId}' was not found.")
    {
        SnapshotId = snapshotId;
    }
}
