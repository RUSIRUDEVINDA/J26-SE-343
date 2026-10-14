namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;

/// <summary>
/// Command invoked when an asynchronous worker actually begins processing an analysis run.
/// Transitions the run state from Requested to Running.
/// </summary>
public sealed record StartDocumentAnalysisRunCommand(
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    int ExpectedRevision
) : ICommand;
