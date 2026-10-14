namespace StateLandGovernance.WorkflowGovernance.Domain.Documents.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record DocumentRegistered(
    Guid EventId,
    DateTime OccurredOn,
    GovernedDocumentId DocumentId,
    LeaseCaseId LeaseCaseId,
    DocumentVersionId DocumentVersionId,
    int VersionNumber,
    int DocumentRevision) : IDomainEvent;
