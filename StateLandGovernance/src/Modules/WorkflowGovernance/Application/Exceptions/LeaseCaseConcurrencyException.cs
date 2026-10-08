namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class LeaseCaseConcurrencyException : Exception
{
    public Guid LeaseCaseId { get; }
    public int ExpectedRevision { get; }
    public int ActualRevision { get; }

    public LeaseCaseConcurrencyException(Guid leaseCaseId, int expectedRevision, int actualRevision)
        : base($"LeaseCase '{leaseCaseId}' revision mismatch. Expected {expectedRevision} but found {actualRevision}.")
    {
        LeaseCaseId = leaseCaseId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }
}
