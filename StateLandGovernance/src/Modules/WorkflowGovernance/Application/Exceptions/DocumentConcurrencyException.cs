namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

/// <summary>
/// Exception thrown when optimistic concurrency detects a stale revision on a governed document.
/// Corresponds to an HTTP 409 Conflict condition at the presentation boundary.
/// </summary>
public sealed class DocumentConcurrencyException : Exception
{
    public Guid GovernedDocumentId { get; }
    public int ExpectedRevision { get; }
    public int ActualRevision { get; }

    public DocumentConcurrencyException(Guid governedDocumentId, int expectedRevision, int actualRevision)
        : base($"Governed document '{governedDocumentId}' revision mismatch. Expected {expectedRevision} but found {actualRevision}.")
    {
        GovernedDocumentId = governedDocumentId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }
}
