namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only DTO exposing case-associated governed document state.
/// </summary>
public sealed record GovernedDocumentDto(
    Guid Id,
    Guid LeaseCaseId,
    string LogicalCategory,
    Guid ActiveVersionId,
    int Revision,
    int VersionCount,
    DocumentVersionDto ActiveVersion,
    IReadOnlyList<DocumentVersionDto> Versions
);

/// <summary>
/// Read-only DTO exposing immutable document version metadata.
/// Internal storage locators (ContentReference) are omitted to protect server-side storage topology.
/// </summary>
public sealed record DocumentVersionDto(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    string OriginalFileName,
    string MediaType,
    long FileSizeInBytes,
    Guid SubmittedByActorId,
    DateTime SubmittedAt
);
