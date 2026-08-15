using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Validators;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class SearchLandRecommendationsQueryHandler
    : IQueryHandler<SearchLandRecommendationsQuery, LandRecommendationSearchResponse>
{
    private readonly ILandRecommendationEngine _recommendationEngine;
    private readonly LandRecommendationSearchRequestValidator _validator;

    public SearchLandRecommendationsQueryHandler(
        ILandRecommendationEngine recommendationEngine,
        LandRecommendationSearchRequestValidator validator)
    {
        _recommendationEngine = recommendationEngine;
        _validator = validator;
    }

    public async Task<LandRecommendationSearchResponse> HandleAsync(
        SearchLandRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(query.Request);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        return await _recommendationEngine.RecommendAsync(query.Request, cancellationToken);
    }
}
