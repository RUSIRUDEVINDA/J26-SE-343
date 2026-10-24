namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;

public sealed record GetApplicantCaseTrackingQuery(
    string ApplicationReference,
    Guid OwnerActorId
) : IQuery<ApplicantCaseTrackingDto?>;

public sealed record ApplicantCaseTrackingDto(
    Guid LeaseCaseId,
    string ApplicationReference,
    string Status,
    DateTime CreatedAt,
    string CurrentMilestone,
    int CompletedMilestonesCount,
    int TotalMilestonesCount,
    IReadOnlyList<ApplicantMilestoneDto> Milestones,
    IReadOnlyList<ApplicantDocumentStatusDto> Documents,
    IReadOnlyList<ApplicantCorrectionNoticeDto> CorrectionNotices
);

public sealed record ApplicantMilestoneDto(
    string MilestoneKey,
    string Title,
    string Description,
    string State, // Completed, InProgress, Pending, AttentionRequired
    DateTime? UpdatedAt
);

public sealed record ApplicantDocumentStatusDto(
    Guid DocumentId,
    string LogicalCategory,
    string OriginalFileName,
    string ProcessingStatus, // Uploaded, Processing, Verified, Rejected
    DateTime UploadedAt
);

public sealed record ApplicantCorrectionNoticeDto(
    Guid RequestId,
    string RequiredAction,
    string? RequiredClassificationCode,
    string? RequiredSectionId,
    string Instructions,
    DateTime RequestedAt,
    DateTime? DueDate,
    string Status,
    int Revision = 0
);
