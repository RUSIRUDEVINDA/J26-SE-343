using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Explainable land recommendation contract for Component 1.
/// Rule-based implementation today; ML/AI implementations can replace via DI without changing callers.
/// </summary>
public interface ILandRecommendationEngine
{
    Task<LandRecommendationSearchResponse> RecommendAsync(
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default);
}
