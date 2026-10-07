namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class ScreeningSnapshotMismatchException : Exception
{
    public Guid LeaseCaseId { get; }
    public Guid SuppliedSnapshotId { get; }
    public Guid PendingSnapshotId { get; }
    public Guid CurrentSnapshotId { get; }

    public ScreeningSnapshotMismatchException(
        Guid leaseCaseId,
        Guid suppliedSnapshotId,
        Guid pendingSnapshotId,
        Guid currentSnapshotId)
        : base($"Screening result specified snapshot ID '{suppliedSnapshotId}', which does not match active pending snapshot ID '{pendingSnapshotId}' or current case snapshot ID '{currentSnapshotId}' for LeaseCase '{leaseCaseId}'.")
    {
        LeaseCaseId = leaseCaseId;
        SuppliedSnapshotId = suppliedSnapshotId;
        PendingSnapshotId = pendingSnapshotId;
        CurrentSnapshotId = currentSnapshotId;
    }
}
