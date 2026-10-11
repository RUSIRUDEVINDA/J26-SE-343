using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Scoped per recommendation run: source-reported cadastre metadata for scoring-only criteria.
/// </summary>
public interface IRecommendationParcelContext
{
    void Load(IReadOnlyDictionary<Guid, CadastralSourceMetadataDto> sourcesByParcelId);

    CadastralSourceMetadataDto? GetSource(Guid parcelId);

    void Clear();
}
