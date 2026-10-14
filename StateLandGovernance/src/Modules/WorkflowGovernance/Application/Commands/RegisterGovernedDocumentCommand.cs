namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;

public sealed record RegisterGovernedDocumentCommand(
    Guid LeaseCaseId,
    string LogicalCategory,
    DocumentContentReceipt ContentReceipt,
    Guid ActorId,
    VerifiedAuthorityContext AuthorityContext,
    Guid? GovernedDocumentId = null,
    Guid? InitialVersionId = null
) : ICommand
{
    public RegisterGovernedDocumentCommand(
        Guid leaseCaseId,
        string logicalCategory,
        DocumentContentReceipt contentReceipt,
        Guid actorId,
        VerifiedAuthoritySnapshot authoritySnapshot,
        Guid? governedDocumentId = null,
        Guid? initialVersionId = null)
        : this(
            leaseCaseId,
            logicalCategory,
            contentReceipt,
            actorId,
            authoritySnapshot != null ? VerifiedAuthorityContext.FromDomain(authoritySnapshot) : null!,
            governedDocumentId,
            initialVersionId)
    {
    }
}
