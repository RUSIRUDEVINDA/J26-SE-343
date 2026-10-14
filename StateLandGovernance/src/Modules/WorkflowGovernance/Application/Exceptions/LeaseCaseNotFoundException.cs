namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class LeaseCaseNotFoundException : Exception
{
    public Guid LeaseCaseId { get; }

    public LeaseCaseNotFoundException(Guid leaseCaseId)
        : base($"Lease case '{leaseCaseId}' was not found.")
    {
        LeaseCaseId = leaseCaseId;
    }
}
