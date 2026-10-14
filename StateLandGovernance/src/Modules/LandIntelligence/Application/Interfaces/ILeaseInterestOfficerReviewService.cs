using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestOfficerReviewService
{
    Task<LeaseInterestOfficerRequestDetailDto> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<LeaseInterestOfficerQueueResponseDto> ListQueueAsync(
        LeaseInterestOfficerQueueRequestDto request,
        CancellationToken cancellationToken = default);

    Task<LeaseInterestOfficerRequestDetailDto> UpdateStatusAsync(
        Guid requestId,
        LeaseInterestOfficerStatusUpdateDto update,
        CancellationToken cancellationToken = default);
}
