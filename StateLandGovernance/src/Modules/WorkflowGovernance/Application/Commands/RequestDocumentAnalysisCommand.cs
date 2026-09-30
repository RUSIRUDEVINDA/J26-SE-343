namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;

/// <summary>
/// Command to request an analysis run for an exact immutable document version.
/// Creates or loads the DocumentAnalysis aggregate and transitions the new run to Requested state.
/// Does NOT begin worker execution or transition the run to Running state.
/// </summary>
public sealed record RequestDocumentAnalysisCommand(
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    string ModelProvider,
    string ModelName,
    string ModelVersion,
    IReadOnlyList<string> RequestedCapabilities,
    int? ExpectedAnalysisRevision = null,
    Guid? AnalysisRunId = null,
    Guid? DocumentAnalysisId = null
) : ICommand;
