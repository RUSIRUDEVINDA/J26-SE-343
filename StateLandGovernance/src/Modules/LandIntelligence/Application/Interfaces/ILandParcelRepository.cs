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

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct GIS-derived soil group names for parcels in the optional district (case-insensitive).
    /// Empty when the district has no imported soil coverage.
    /// </summary>
    Task<IReadOnlyList<string>> ListDerivedSoilGroupOptionsAsync(
        string? district = null,
        CancellationToken cancellationToken = default);
}
