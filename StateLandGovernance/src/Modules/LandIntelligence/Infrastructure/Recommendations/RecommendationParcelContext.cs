using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

public sealed class RecommendationParcelContext : IRecommendationParcelContext
{
    private IReadOnlyDictionary<Guid, CadastralSourceMetadataDto> _sources =
        new Dictionary<Guid, CadastralSourceMetadataDto>();

    public void Load(IReadOnlyDictionary<Guid, CadastralSourceMetadataDto> sourcesByParcelId) =>
        _sources = sourcesByParcelId ?? new Dictionary<Guid, CadastralSourceMetadataDto>();

    public CadastralSourceMetadataDto? GetSource(Guid parcelId) =>
        _sources.TryGetValue(parcelId, out var source) ? source : null;

    public void Clear() =>
        _sources = new Dictionary<Guid, CadastralSourceMetadataDto>();
}
