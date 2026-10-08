namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;

/// <summary>
/// Technical execution result returned by the document analysis execution coordinator.
/// Exposes run identifier, terminal execution status, artifact count, and sanitized failure metadata.
/// </summary>
public sealed record DocumentAnalysisExecutionResult(
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    string Status,
    int ArtifactCount,
    string? FailureCode = null,
    string? FailureDescription = null
);
