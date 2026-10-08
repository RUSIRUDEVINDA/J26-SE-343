namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Integration DTO dispatched to Component 4 containing only human-verified facts from an authoritative snapshot.
/// Raw OCR, candidate extractions, and unverified data are never included.
/// Correlated via ScreeningRequestId which matches the active Pending ScreeningResult.Id.
/// </summary>
public sealed record Component4ScreeningRequest(
    Guid ScreeningRequestId,
    Guid LeaseCaseId,
    string ApplicationReference,
    Guid VerifiedFactSnapshotId,
    IReadOnlyList<ScreeningFactEntryDto> Facts,
    DateTime RequestedAtUtc
);

/// <summary>
/// Verified fact entry dispatched to Component 4 for regulatory and institutional assessment.
/// </summary>
public sealed record ScreeningFactEntryDto(
    Guid SourceExtractedFactId,
    string FactCode,
    string CanonicalValue,
    string ValueKind,
    string Decision,
    Guid VerifyingActorId,
    DateTime VerifiedAt
);
