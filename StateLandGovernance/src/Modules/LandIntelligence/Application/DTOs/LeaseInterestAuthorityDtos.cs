using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Authority queue filter. Pending = referred with an officer valuation (default);
/// Approved = approved in principle; Rejected = declined after valuation or authority decision.
/// </summary>
public enum LeaseInterestAuthorityQueueFilter
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    All = 3,
}

public static class LeaseInterestAuthorityQueueFilters
{
    /// <summary>Parses an optional filter string; blank defaults to Pending. Returns false for unknown values.</summary>
    public static bool TryParse(string? value, out LeaseInterestAuthorityQueueFilter filter)
    {
        filter = LeaseInterestAuthorityQueueFilter.Pending;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out filter)
            && Enum.IsDefined(filter)
            && !int.TryParse(value.Trim(), out _);
    }
}

public sealed record LeaseInterestAuthorityQueueRequestDto(
    int Page = 1,
    int PageSize = 20,
    /// <summary>Optional: Pending | Approved | Rejected | All. Defaults to Pending.</summary>
    string? StatusFilter = null);

public sealed record LeaseInterestAuthorityQueueItemDto(
    Guid RequestId,
    Guid LandParcelId,
    string? AcknowledgementReference,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? LatestValuationAtUtc,
    int LatestValuationRevision,
    LeaseInterestRequestStatus Status,
    string? CadastralNumber,
    string? District,
    LeaseInterestApplicantKindDto ApplicantKind,
    string? NationalIdentityCardNumber,
    string? BusinessRegistrationNumber);

public sealed record LeaseInterestAuthorityQueueResponseDto(
    IReadOnlyList<LeaseInterestAuthorityQueueItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RecordLeaseInterestAuthorityDecisionDto(
    LeaseInterestAuthorityDecisionKind Decision,
    string Reason,
    /// <summary>Client-observed UpdatedAtUtc for optimistic concurrency.</summary>
    DateTimeOffset ExpectedUpdatedAtUtc,
    /// <summary>The exact valuation the authority reviewed before deciding.</summary>
    Guid ReviewedValuationId,
    int ReviewedValuationRevision);

public sealed record LeaseInterestAuthorityDecisionDto(
    Guid Id,
    Guid LeaseInterestRequestId,
    LeaseInterestAuthorityDecisionKind Decision,
    string Reason,
    Guid? ReviewedValuationId,
    int? ReviewedValuationRevision,
    Guid RecordedByAuthorityId,
    DateTimeOffset RecordedAtUtc,
    LeaseInterestRequestStatus ResultingStatus);

public sealed record LeaseInterestAuthorityRequestDetailDto(
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
    LeaseInterestValuationDto? LatestValuation,
    IReadOnlyList<LeaseInterestValuationDto> Valuations,
    IReadOnlyList<LeaseInterestAuthorityDecisionDto> AuthorityDecisions,
    bool CanRecordDecision);
