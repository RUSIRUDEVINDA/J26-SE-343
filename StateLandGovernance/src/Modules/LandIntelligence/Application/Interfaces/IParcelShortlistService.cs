using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IParcelShortlistService
{
    Task<IReadOnlyList<ParcelShortlistItemDto>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(Guid landParcelId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid landParcelId, CancellationToken cancellationToken);
}
