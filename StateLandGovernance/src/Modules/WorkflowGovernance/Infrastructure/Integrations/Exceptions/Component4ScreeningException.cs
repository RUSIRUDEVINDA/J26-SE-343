namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;

using System;

/// <summary>
/// Thrown when Component 4 screening evaluation fails due to an integration, engine, or infrastructure fault.
/// In accordance with Component 3 governance invariants, technical evaluation failures never mutate state to Cleared.
/// The screening remains Pending or Blocked to protect the workflow progression gate.
/// </summary>
public sealed class Component4ScreeningException : Exception
{
    public Guid ScreeningRequestId { get; }
    public Guid LeaseCaseId { get; }

    public Component4ScreeningException(Guid screeningRequestId, Guid leaseCaseId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ScreeningRequestId = screeningRequestId;
        LeaseCaseId = leaseCaseId;
    }
}

