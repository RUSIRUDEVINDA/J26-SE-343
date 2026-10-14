namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class ScreeningSnapshotCaseMismatchException : Exception
{
    public Guid TargetLeaseCaseId { get; }
    public Guid SnapshotId { get; }
    public Guid OwningLeaseCaseId { get; }

    public ScreeningSnapshotCaseMismatchException(Guid targetLeaseCaseId, Guid snapshotId, Guid owningLeaseCaseId)
        : base($"VerifiedFactSnapshot '{snapshotId}' belongs to LeaseCase '{owningLeaseCaseId}' and cannot be used for LeaseCase '{targetLeaseCaseId}'.")
    {
        TargetLeaseCaseId = targetLeaseCaseId;
        SnapshotId = snapshotId;
        OwningLeaseCaseId = owningLeaseCaseId;
    }
}
