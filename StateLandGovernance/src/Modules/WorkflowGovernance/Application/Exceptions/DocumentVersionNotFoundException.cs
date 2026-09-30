namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class DocumentVersionNotFoundException : Exception
{
    public Guid GovernedDocumentId { get; }
    public Guid DocumentVersionId { get; }

    public DocumentVersionNotFoundException(Guid governedDocumentId, Guid documentVersionId)
        : base($"DocumentVersion '{documentVersionId}' was not found in GovernedDocument '{governedDocumentId}'.")
    {
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
    }
}
