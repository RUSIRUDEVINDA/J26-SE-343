namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;

/// <summary>
/// Command to mark an analysis run failed with sanitized machine failure metadata.
/// </summary>
public sealed record FailDocumentAnalysisCommand(
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    int ExpectedRevision,
    string FailureCode,
    string SafeDescription
) : ICommand;
