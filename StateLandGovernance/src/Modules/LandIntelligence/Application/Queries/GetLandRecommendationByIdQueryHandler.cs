using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetLandRecommendationByIdQueryHandler
    : IQueryHandler<GetLandRecommendationByIdQuery, LandRecommendationDto>
{
    private readonly ILandRecommendationRepository _landRecommendationRepository;

    public GetLandRecommendationByIdQueryHandler(ILandRecommendationRepository landRecommendationRepository)
    {
        _landRecommendationRepository = landRecommendationRepository;
    }

    public async Task<LandRecommendationDto> HandleAsync(
        GetLandRecommendationByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RecommendationId == Guid.Empty)
        {
            throw new ValidationException(["Recommendation identifier is required."]);
        }

        var recommendation = await _landRecommendationRepository.GetByIdAsync(query.RecommendationId, cancellationToken)
            ?? throw new LandRecommendationNotFoundException(query.RecommendationId);

        return LandRecommendationMapper.ToDto(recommendation);
    }
}
