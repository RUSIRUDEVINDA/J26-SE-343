namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

/// <summary>
/// Exception thrown when a governed document is not found.
/// Corresponds to an HTTP 404 Not Found condition at the presentation boundary.
/// </summary>
public sealed class GovernedDocumentNotFoundException : Exception
{
    public Guid GovernedDocumentId { get; }

    public GovernedDocumentNotFoundException(Guid governedDocumentId)
        : base($"Governed document '{governedDocumentId}' was not found.")
    {
        GovernedDocumentId = governedDocumentId;
    }
}
