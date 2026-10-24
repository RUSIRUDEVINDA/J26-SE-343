using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestOfficerLookupService
{
    Task<LeaseInterestOfficerLookupResponseDto> LookupByNicAsync(
        LeaseInterestOfficerLookupRequestDto request,
        CancellationToken cancellationToken);
}
