namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

using StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Read-only access to persisted real-parcel source metadata (land_intelligence.real_parcel_sources).
/// </summary>
public interface IRealParcelSourceReadRepository
{
    Task<CadastralSourceMetadataDto?> GetByParcelIdAsync(Guid landParcelId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, CadastralSourceMetadataDto>> GetByParcelIdsAsync(
        IReadOnlyCollection<Guid> landParcelIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceReportedLandUseOptionDto>> ListSourceReportedLandUseOptionsAsync(
        CancellationToken cancellationToken = default);
}
