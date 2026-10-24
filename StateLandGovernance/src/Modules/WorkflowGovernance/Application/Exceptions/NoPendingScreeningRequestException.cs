namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class NoPendingScreeningRequestException : Exception
{
    public Guid LeaseCaseId { get; }

    public NoPendingScreeningRequestException(Guid leaseCaseId)
        : base($"Lease case '{leaseCaseId}' does not have an active Pending screening request to receive results.")
    {
        LeaseCaseId = leaseCaseId;
    }
}
