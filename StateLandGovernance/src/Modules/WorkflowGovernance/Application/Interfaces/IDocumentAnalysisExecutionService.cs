namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Execution coordinator port for orchestrating an end-to-end analysis run:
/// claiming the run, verifying exact document version integrity, invoking external
/// document intelligence, persisting artifacts, and recording completion or failure.
/// </summary>
public interface IDocumentAnalysisExecutionService
{
    Task<DocumentAnalysisExecutionResult> ExecuteAsync(
        Guid documentAnalysisId,
        Guid analysisRunId,
        int expectedAnalysisRevision,
        string? languageHint = null,
        CancellationToken cancellationToken = default);
}
