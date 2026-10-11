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
    string? OrganizationRegistrationNumber);

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
    string? Note);

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
    int? RequestedDurationYears);

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
    LeaseInterestCostSummaryDto CostSummary);

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
