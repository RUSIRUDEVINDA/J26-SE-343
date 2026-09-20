using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Builds knowledge-graph read models from authoritative PostgreSQL/PostGIS persistence.
/// </summary>
public interface IPostGisKnowledgeGraphBaselineProvider
{
    Task<LandParcelGisGraphIntelligenceDto?> GetGisGraphIntelligenceAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
        Guid soilGroupReferenceId,
        CancellationToken cancellationToken = default);
}
