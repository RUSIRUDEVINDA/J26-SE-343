namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class ScreeningRequestMismatchException : Exception
{
    public Guid LeaseCaseId { get; }
    public Guid SuppliedScreeningRequestId { get; }
    public Guid ActiveScreeningRequestId { get; }

    public ScreeningRequestMismatchException(Guid leaseCaseId, Guid suppliedScreeningRequestId, Guid activeScreeningRequestId)
        : base($"Screening result specified request ID '{suppliedScreeningRequestId}', which does not match active pending request ID '{activeScreeningRequestId}' for LeaseCase '{leaseCaseId}'.")
    {
        LeaseCaseId = leaseCaseId;
        SuppliedScreeningRequestId = suppliedScreeningRequestId;
        ActiveScreeningRequestId = activeScreeningRequestId;
    }
}
