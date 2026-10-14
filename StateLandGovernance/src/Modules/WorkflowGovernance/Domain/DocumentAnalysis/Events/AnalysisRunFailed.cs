namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record AnalysisRunFailed(
    Guid EventId,
    DateTime OccurredOn,
    DocumentAnalysisId DocumentAnalysisId,
    AnalysisRunId AnalysisRunId,
    GovernedDocumentId GovernedDocumentId,
    DocumentVersionId DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    int RunNumber,
    string FailureCode,
    string FailureDescription,
    int DocumentAnalysisRevision) : IDomainEvent;
