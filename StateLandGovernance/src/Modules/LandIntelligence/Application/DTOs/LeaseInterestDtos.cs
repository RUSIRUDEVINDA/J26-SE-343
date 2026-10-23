using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum LeaseInterestApplicantKindDto
{
    Individual = 1,
    Organization = 2,
}

public sealed record LeaseInterestApplicantDto(
    LeaseInterestApplicantKindDto Kind,
    string LegalName,
    string ContactPhone,
    string ContactEmail,
    string PostalAddress,
    string? OrganizationName,
    string? OrganizationRegistrationNumber,
    string? NationalIdentityCardNumber,
    string? RepresentativeNationalIdentityCardNumber);

public sealed record LeaseInterestProposalDto(
    LandUseType RequestedPurpose,
    string PlannedActivitiesDescription,
    decimal? RequestedAreaHectares,
    string DesiredStartPeriod,
    int RequestedDurationYears,
    string ProjectSummary);

public sealed record LeaseInterestSupportingInfoDto(
    string SupportingNotes);

public sealed record UpsertLeaseInterestDraftDto(
    LeaseInterestApplicantDto Applicant,
    LeaseInterestProposalDto Proposal,
    LeaseInterestSupportingInfoDto Supporting,
    bool ConsentToPreliminaryAssessment);

public sealed record LeaseInterestStatusHistoryEntryDto(
    LeaseInterestRequestStatus Status,
    DateTimeOffset ChangedAtUtc,
    string? Note,
    LeaseInterestStatusActorKindDto ActorKind = LeaseInterestStatusActorKindDto.System,
    Guid? ActorId = null,
    bool VisibleToApplicant = true);

public enum LeaseInterestStatusActorKindDto
{
    System = 0,
    Applicant = 1,
    Officer = 2,
    Authority = 3,
}

/// <summary>Applicant-visible authority decision summary. Excludes internal officer notes.</summary>
public sealed record LeaseInterestApplicantAuthorityDecisionDto(
    LeaseInterestAuthorityDecisionKind Decision,
    string ApplicantFacingReason,
    DateTimeOffset RecordedAtUtc,
    string DecisionLabel,
    string? NextActionHint);

public sealed record LeaseInterestCostLineDto(
    string Code,
    string Label,
    string Status,
    decimal? Amount,
    string? Currency,
    string? RateSource,
    DateTimeOffset? EffectiveDate,
    string? Formula);

public sealed record LeaseInterestCostSummaryDto(
    string Currency,
    string Disclaimer,
    bool NumericEstimateAvailable,
    IReadOnlyList<LeaseInterestCostLineDto> Lines);

public sealed record LeaseInterestRequestSummaryDto(
    Guid Id,
    Guid LandParcelId,
    LeaseInterestRequestStatus Status,
    string? AcknowledgementReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    LandUseType RequestedPurpose,
    int? RequestedDurationYears,
    string? CadastralNumber = null);

public sealed record LeaseInterestRequestDetailDto(
    Guid Id,
    Guid LandParcelId,
    LeaseInterestRequestStatus Status,
    string? AcknowledgementReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    LeaseInterestApplicantDto Applicant,
    LeaseInterestProposalDto Proposal,
    LeaseInterestSupportingInfoDto Supporting,
    bool ConsentToPreliminaryAssessment,
    IReadOnlyList<LeaseInterestStatusHistoryEntryDto> StatusHistory,
    LeaseInterestCostSummaryDto CostSummary,
    LeaseInterestApplicantAuthorityDecisionDto? LatestAuthorityDecision = null);

public sealed record ParcelShortlistItemDto(
    Guid LandParcelId,
    DateTimeOffset SavedAtUtc,
    string? CadastralNumber,
    string? District,
    decimal? AreaHectares);

public sealed record LeaseInterestDocumentChecklistItemDto(
    string Code,
    string Label,
    string Note);

public sealed record LeaseInterestDocumentChecklistDto(
    IReadOnlyList<LeaseInterestDocumentChecklistItemDto> Items,
    string StorageNote);

public sealed record LeaseInterestOfficerLookupRequestDto(
    string? NationalIdentityCardNumber,
    string? BusinessRegistrationNumber,
    int Page = 1,
    int PageSize = 20);

public sealed record LeaseInterestOfficerLookupItemDto(
    Guid RequestId,
    Guid LandParcelId,
    string? AcknowledgementReference,
    DateTimeOffset? SubmittedAtUtc,
    LeaseInterestRequestStatus Status,
    string? CadastralNumber,
    string? District,
    LandUseType RequestedPurpose,
    int? RequestedDurationYears,
    LeaseInterestApplicantKindDto ApplicantKind,
    string? NationalIdentityCardNumber,
    string? BusinessRegistrationNumber);

public sealed record LeaseInterestOfficerLookupResponseDto(
    IReadOnlyList<LeaseInterestOfficerLookupItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record LeaseInterestOfficerQueueRequestDto(
    int Page = 1,
    int PageSize = 20);

public sealed record LeaseInterestOfficerQueueItemDto(
    Guid RequestId,
    Guid LandParcelId,
    string? AcknowledgementReference,
    DateTimeOffset? SubmittedAtUtc,
    LeaseInterestRequestStatus Status,
    string? CadastralNumber,
    string? District);

public sealed record LeaseInterestOfficerQueueResponseDto(
    IReadOnlyList<LeaseInterestOfficerQueueItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record LeaseInterestOfficerStatusUpdateDto(
    LeaseInterestRequestStatus NewStatus,
    string Note,
    bool VisibleToApplicant = true);

public sealed record LeaseInterestOfficerRequestDetailDto(
    Guid Id,
    Guid LandParcelId,
    LeaseInterestRequestStatus Status,
    string? AcknowledgementReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    LeaseInterestApplicantDto Applicant,
    LeaseInterestProposalDto Proposal,
    LeaseInterestSupportingInfoDto Supporting,
    bool ConsentToPreliminaryAssessment,
    IReadOnlyList<LeaseInterestStatusHistoryEntryDto> StatusHistory,
    string? CadastralNumber,
    string? District,
    string ApplicantOutcomeSummary,
    LeaseInterestApplicantAuthorityDecisionDto? LatestAuthorityDecision = null);
