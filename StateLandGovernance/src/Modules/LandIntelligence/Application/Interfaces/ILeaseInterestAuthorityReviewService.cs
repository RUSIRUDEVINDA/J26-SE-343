using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestAuthorityReviewService
{
    Task<LeaseInterestAuthorityQueueResponseDto> ListQueueAsync(
        LeaseInterestAuthorityQueueRequestDto request,
        CancellationToken cancellationToken = default);

    Task<LeaseInterestAuthorityRequestDetailDto> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<LeaseInterestAuthorityRequestDetailDto> RecordDecisionAsync(
        Guid requestId,
        RecordLeaseInterestAuthorityDecisionDto input,
        CancellationToken cancellationToken = default);
}
