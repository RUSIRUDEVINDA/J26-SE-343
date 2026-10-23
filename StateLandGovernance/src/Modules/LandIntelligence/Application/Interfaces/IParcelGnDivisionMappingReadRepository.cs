using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IParcelGnDivisionMappingReadRepository
{
    Task<ParcelGnDivisionMappingDto?> GetByCadastralNumberAsync(
        string cadastralNumber,
        CancellationToken cancellationToken = default);

    Task<ParcelGnDivisionMappingDto?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListDistinctGnDivisionNamesAsync(
        string? district = null,
        CancellationToken cancellationToken = default);
}
