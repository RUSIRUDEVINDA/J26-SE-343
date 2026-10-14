namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class MissingCurrentVerifiedFactSnapshotException : Exception
{
    public Guid LeaseCaseId { get; }

    public MissingCurrentVerifiedFactSnapshotException(Guid leaseCaseId)
        : base($"Lease case '{leaseCaseId}' does not have an authoritative CurrentVerifiedFactSnapshot linked. Screening requires verified facts.")
    {
        LeaseCaseId = leaseCaseId;
    }
}
