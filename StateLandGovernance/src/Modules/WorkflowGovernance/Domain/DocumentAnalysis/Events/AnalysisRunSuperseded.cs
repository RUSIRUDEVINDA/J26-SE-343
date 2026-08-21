namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record AnalysisRunSuperseded(
    Guid EventId,
    DateTime OccurredOn,
    DocumentAnalysisId DocumentAnalysisId,
    AnalysisRunId AnalysisRunId,
    GovernedDocumentId GovernedDocumentId,
    DocumentVersionId DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    int RunNumber,
    string SupersessionReason,
    int DocumentAnalysisRevision) : IDomainEvent;
