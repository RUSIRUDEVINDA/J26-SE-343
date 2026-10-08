using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class GenerateLandRecommendationCommandHandler
    : ICommandHandler<GenerateLandRecommendationCommand, LandRecommendationDto>
{
    private readonly ILandRecommendationEngine _recommendationEngine;
    private readonly ILandRecommendationRepository _landRecommendationRepository;
    private readonly LandRecommendationRequestValidator _validator;

    public GenerateLandRecommendationCommandHandler(
        ILandRecommendationEngine recommendationEngine,
        ILandRecommendationRepository landRecommendationRepository,
        LandRecommendationRequestValidator validator)
    {
        _recommendationEngine = recommendationEngine;
        _landRecommendationRepository = landRecommendationRepository;
        _validator = validator;
    }

    public async Task<LandRecommendationDto> HandleAsync(
        GenerateLandRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var request = command.Request;
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var searchRequest = new LandRecommendationSearchRequest
        {
            RequiredPurpose = request.TargetLandUseType ?? LandUseType.Other,
            TargetParcelId = request.LandParcelId,
            RequiredLandUse = request.TargetLandUseType,
            MaxResults = 1
        };

        var response = await _recommendationEngine.RecommendAsync(searchRequest, cancellationToken);
        var result = response.Recommendations.FirstOrDefault()
            ?? throw new LandParcelNotFoundException(request.LandParcelId);

        var recommendation = MapToDomainRecommendation(result, request);

        if (request.FinalizeRecommendation)
        {
            recommendation.FinalizeRecommendation();
        }

        await _landRecommendationRepository.AddAsync(recommendation, cancellationToken);

        return LandRecommendationMapper.ToDto(recommendation);
    }

    private static LandRecommendation MapToDomainRecommendation(
        LandParcelRecommendationResult result,
        LandRecommendationRequest request)
    {
        var recommendedUse = request.TargetLandUseType is not null
            ? new LandUse(request.TargetLandUseType.Value, request.TargetLandUseDescription)
            : null;

        var recommendation = new LandRecommendation(
            result.ParcelId,
            result.SuitabilityScore,
            recommendedUse);

        recommendation.AssignRank(result.Rank);

        foreach (var criterion in result.MatchingCriteria.Concat(result.FailedCriteria))
        {
            recommendation.AddCriterion(new RecommendationCriterion(
                criterion.Category,
                criterion.Name,
                criterion.Weight,
                criterion.Score,
                criterion.Summary));
        }

        foreach (var evidence in result.Evidence)
        {
            recommendation.AddEvidence(new RecommendationEvidence(
                evidence.Source,
                evidence.Description,
                evidence.RelatedCriterionName));
        }

        recommendation.AddEvidence(new RecommendationEvidence(
            "RecommendationExplanation",
            result.Explanation,
            null));

        return recommendation;
    }
}
