namespace StateLandGovernance.WorkflowGovernance.Presentation.DTOs;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record RegisterCaseIntakeRequest(
    string ApplicationReference,
    string? Purpose,
    decimal? RequestedExtent,
    string? RequestedExtentUnit,
    string? JurisdictionCode,
    string? LanguageMode = "sin"
);

public sealed record RegisterCaseIntakeResponse(
    Guid LeaseCaseId,
    string ApplicationReference,
    string Status,
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    Guid? DocumentAnalysisId,
    Guid? AnalysisRunId,
    string AnalysisExecutionStatus,
    int ExtractedCandidatesCount,
    string? AnalysisFailureReason = null
);

public sealed record CaseDetailResponse(
    Guid LeaseCaseId,
    string ApplicationReference,
    Guid CreatedByActorId,
    DateTime CreatedAt,
    string Status,
    int Revision,
    Guid? CurrentVerifiedFactSnapshotId,
    string? Purpose,
    decimal? RequestedExtent,
    string? RequestedExtentUnit,
    string? JurisdictionCode,
    IReadOnlyList<CaseDocumentSummaryDto> Documents,
    CaseVerifiedSnapshotSummaryDto? Snapshot,
    IReadOnlyList<CaseGovernedFieldRowDto> FieldInventory,
    ProposalWorkspaceSelectionDto? ProposalSelection = null,
    bool CanVerifyFacts = false,
    bool CanPublishSnapshot = false
);

public sealed record ProposalWorkspaceSelectionDto(Guid? DocumentId, Guid? VersionId, Guid? AnalysisId,
    Guid? RunId, Guid? RunResultId, string Status, string? Notice, bool LinkedSnapshotMatchesSelection = false);

public sealed record CaseDocumentSummaryDto(
    Guid DocumentId,
    string LogicalCategory,
    Guid ActiveVersionId,
    int VersionNumber,
    string OriginalFileName,
    string MediaType,
    long FileSizeBytes,
    string ChecksumValue,
    DateTime CreatedAt,
    Guid? DocumentAnalysisId,
    int DocumentAnalysisRevision,
    Guid? LatestRunResultId,
    int AnalysisRunCount,
    string? LatestRunState,
    int LatestRunCandidatesCount,
    string? LatestRunFailureCode = null,
    string? LatestRunFailureDescription = null
);

public sealed record CaseVerifiedSnapshotSummaryDto(
    Guid SnapshotId,
    Guid DocumentAnalysisId,
    string SnapshotOutcome,
    DateTime PublishedAt,
    Guid PublishingActorId,
    int PublishedFactCount,
    int ConfirmedFactCount,
    int CorrectedFactCount
);

public sealed record CaseGovernedFieldRowDto(
    string FactCode,
    string DisplayName,
    string Status, // Extracted, Ungrounded, NoCandidate, Missing, Unavailable, Confirmed, Corrected, Rejected, Unsupported, Verified
    string? CandidateValue,
    string? PreferredValue,
    decimal? ConfidenceScore,
    int? PageNumber,
    string? EvidenceQuote,
    Guid? ExtractedFactId,
    Guid? VerificationId,
    string? OfficerDecision,
    string? OfficerReason,
    bool IsGrounded = true,
    string? SourceKind = null,
    Guid? HumanEvidenceObservationId = null
);

public sealed record VerifyFactApiRequest(
    Guid DocumentAnalysisId,
    Guid AnalysisRunResultId,
    Guid ExtractedFactId,
    int ExpectedAnalysisRevision,
    string Decision, // Confirm, Correct, Reject, MarkUnsupported
    string? CorrectedValue,
    string? ValueKind, // Text, Identifier, Integer, Decimal, Date, Boolean (AnalysisFactValueKind)
    string? Reason
);

public sealed record PublishSnapshotApiRequest(
    Guid DocumentAnalysisId,
    Guid AnalysisRunResultId,
    int ExpectedAnalysisRevision,
    int ExpectedLeaseCaseRevision
);

// --- Dual Completeness & Correction DTOs ---

public sealed record AssessDocumentCompletenessApiRequest(
    string? RequirementSetIdentifier = "DOC-REQ-AGRI-V1",
    string? RequirementSetVersion = "1.0"
);

public sealed record AssessProposalContentApiRequest(
    string? TemplateId = "STD-PROP-2026-V1",
    string? TemplateVersion = "1.0",
    Guid? ProposalDocumentId = null
);

public sealed record CaseCompletenessSummaryResponse(
    Guid LeaseCaseId,
    DocumentCompletenessAssessmentDto? DocumentCompleteness,
    ProposalContentAssessmentDto? ProposalContent,
    bool IsDocumentCompletenessCurrent,
    bool IsProposalContentCurrent,
    IReadOnlyList<CaseCorrectionRequestDto> ActiveCorrectionRequests,
    bool DocumentRequirementsConfigured = false,
    bool ProposalTemplateConfigured = false
);

public sealed record CaseCorrectionRequestDto(
    Guid Id,
    Guid LeaseCaseId,
    string RequestedByRole,
    DateTime CreatedAtUtc,
    DateTime? DueDateUtc,
    string Status, // Pending, Responded, Resolved, Waived
    string RequiredAction, // UploadSupportingDocument, ReviseProposal
    string? RequiredClassificationCode,
    string? RequiredSectionId,
    string OfficerInstructions,
    Guid? RespondedDocumentId,
    DateTime? RespondedAtUtc,
    int Revision = 0
);

public sealed record CreateCorrectionApiRequest(
    string RequiredAction, // UploadSupportingDocument, ReviseProposal
    string? RequiredClassificationCode,
    string? RequiredSectionId,
    string OfficerInstructions,
    DateTime? DueDateUtc = null
);

public sealed record RespondToCorrectionApiRequest(
    string? ApplicantResponseNotes
);

public sealed record ReviewCorrectionApiRequest(bool Accept, string Reason, int ExpectedRevision);
