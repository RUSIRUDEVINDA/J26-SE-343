using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILandRecommendationRepository
{
    Task<LandRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandRecommendation>> GetByParcelIdAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);

    Task AddAsync(LandRecommendation recommendation, CancellationToken cancellationToken = default);

    Task UpdateAsync(LandRecommendation recommendation, CancellationToken cancellationToken = default);
}
