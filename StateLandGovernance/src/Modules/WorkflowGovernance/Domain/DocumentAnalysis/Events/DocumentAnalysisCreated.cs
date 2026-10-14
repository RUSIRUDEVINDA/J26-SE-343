namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record DocumentAnalysisCreated(
    Guid EventId,
    DateTime OccurredOn,
    DocumentAnalysisId DocumentAnalysisId,
    GovernedDocumentId GovernedDocumentId,
    DocumentVersionId DocumentVersionId,
    int DocumentVersionNumber,
    int SourceDocumentRevision,
    string ChecksumAlgorithm,
    string ChecksumValue,
    int DocumentAnalysisRevision) : IDomainEvent;
