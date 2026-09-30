namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to complete an analysis run by recording machine-generated artifacts and unverified candidate facts.
/// </summary>
public sealed record CompleteDocumentAnalysisCommand(
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    int ExpectedRevision,
    string Outcome,
    IReadOnlyList<AnalysisArtifactOutputDto> Artifacts,
    IReadOnlyList<ExtractedCandidateFactDto> ExtractedCandidateFacts,
    Guid? ResultId = null
) : ICommand;
