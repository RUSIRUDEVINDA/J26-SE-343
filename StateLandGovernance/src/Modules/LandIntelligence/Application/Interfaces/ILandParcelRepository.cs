using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILandParcelRepository
{
    Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LandParcel?> GetByCadastralNumberAsync(
        string cadastralNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandParcel>> SearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<int> CountSearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default);

    Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default);

    Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default);
}
