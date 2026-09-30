namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Machine-directed analysis request passed across the document intelligence port.
/// Carries only information required for external OCR, classification, and extraction.
/// </summary>
public sealed record DocumentIntelligenceRequest(
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    string ContentReference,
    string ChecksumAlgorithm,
    string ChecksumValue,
    string OriginalFileName,
    string MediaType,
    string LogicalCategory,
    IReadOnlyList<string> RequestedCapabilities,
    string? LanguageHint = null
);
