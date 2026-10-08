namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;

public sealed record LeaseCaseDto(
    Guid Id,
    string ApplicationReference,
    Guid CreatedByActorId,
    DateTime CreatedAt,
    string Status,
    int Revision,
    Guid? CurrentVerifiedFactSnapshotId,
    ProposalIntakeSummaryDto? ProposalIntake
);

public sealed record ProposalIntakeSummaryDto(
    string? Purpose,
    decimal? RequestedExtent,
    string? RequestedExtentUnit,
    string? JurisdictionCode,
    string? SourceReference,
    string? SourceVersion
);
