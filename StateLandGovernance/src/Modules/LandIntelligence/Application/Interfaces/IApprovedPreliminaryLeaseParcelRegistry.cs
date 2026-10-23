using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Authoritative read model for parcels with a current ApprovedInPrinciple lease-interest request.
/// </summary>
public interface IApprovedPreliminaryLeaseParcelRegistry
{
    Task<IReadOnlySet<Guid>> GetApprovedParcelIdsAsync(CancellationToken cancellationToken = default);

    Task<bool> HasApprovedPreliminaryRequestAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);

    Task<ApprovedPreliminaryParcelListResponse> ListApprovedParcelsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
