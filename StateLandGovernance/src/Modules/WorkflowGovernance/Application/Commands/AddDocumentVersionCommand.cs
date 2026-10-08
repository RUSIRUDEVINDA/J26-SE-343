namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;

public sealed record AddDocumentVersionCommand(
    Guid GovernedDocumentId,
    Guid ExpectedPredecessorVersionId,
    DocumentContentReceipt ContentReceipt,
    Guid ActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedRevision,
    Guid? NewVersionId = null
) : ICommand
{
    public AddDocumentVersionCommand(
        Guid governedDocumentId,
        Guid expectedPredecessorVersionId,
        DocumentContentReceipt contentReceipt,
        Guid actorId,
        VerifiedAuthoritySnapshot authoritySnapshot,
        int expectedRevision,
        Guid? newVersionId = null)
        : this(
            governedDocumentId,
            expectedPredecessorVersionId,
            contentReceipt,
            actorId,
            authoritySnapshot != null ? VerifiedAuthorityContext.FromDomain(authoritySnapshot) : null!,
            expectedRevision,
            newVersionId)
    {
    }
}
