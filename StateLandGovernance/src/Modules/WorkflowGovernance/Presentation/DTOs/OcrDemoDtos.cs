namespace StateLandGovernance.WorkflowGovernance.Presentation.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Response returned by the temporary OCR vertical-slice demonstration endpoint.
/// Exposes machine-generated recognition text and metadata for display in the demo UI.
/// Output is machine-generated only and has NOT been verified or promoted to legal facts.
/// </summary>
public sealed record OcrDemoResponse(
    Guid CaseId,
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    string Status,
    string LanguageMode,
    string FileName,
    string MediaType,
    int PageCount,
    string FullText,
    IReadOnlyList<OcrDemoPageResponse> Pages,
    IReadOnlyList<string> Warnings,
    string GovernanceNotice
);

/// <summary>
/// Page-level OCR recognition result for demo display.
/// </summary>
public sealed record OcrDemoPageResponse(
    int PageNumber,
    string RawText,
    string CleanText,
    double? Confidence
);
