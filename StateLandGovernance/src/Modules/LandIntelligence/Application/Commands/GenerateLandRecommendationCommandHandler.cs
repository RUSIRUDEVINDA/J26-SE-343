using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Domain.Services;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class GenerateLandRecommendationCommandHandler
    : ICommandHandler<GenerateLandRecommendationCommand, LandRecommendationDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly ILandRecommendationRepository _landRecommendationRepository;
    private readonly ILandSuitabilityEvaluator _suitabilityEvaluator;
    private readonly LandRecommendationRequestValidator _validator;

    public GenerateLandRecommendationCommandHandler(
        ILandParcelRepository landParcelRepository,
        ILandRecommendationRepository landRecommendationRepository,
        ILandSuitabilityEvaluator suitabilityEvaluator,
        LandRecommendationRequestValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _landRecommendationRepository = landRecommendationRepository;
        _suitabilityEvaluator = suitabilityEvaluator;
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

        var parcel = await _landParcelRepository.GetByIdAsync(request.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(request.LandParcelId);

        var criteria = BuildDefaultCriteria();
        var recommendation = _suitabilityEvaluator.Evaluate(parcel, criteria);

        if (request.TargetLandUseType is not null)
        {
            recommendation = ApplyTargetLandUse(recommendation, request);
        }

        if (request.FinalizeRecommendation)
        {
            recommendation.FinalizeRecommendation();
        }

        await _landRecommendationRepository.AddAsync(recommendation, cancellationToken);

        return LandRecommendationMapper.ToDto(recommendation);
    }

    private static LandRecommendation ApplyTargetLandUse(
        LandRecommendation recommendation,
        LandRecommendationRequest request)
    {
        var targetUse = new LandUse(request.TargetLandUseType!.Value, request.TargetLandUseDescription);
        var adjusted = new LandRecommendation(
            recommendation.LandParcelId,
            recommendation.SuitabilityScore,
            targetUse,
            recommendation.Status);

        foreach (var criterion in recommendation.Criteria)
        {
            adjusted.AddCriterion(criterion);
        }

        foreach (var evidence in recommendation.Evidence)
        {
            adjusted.AddEvidence(evidence);
        }

        return adjusted;
    }

    private static IReadOnlyList<RecommendationCriterion> BuildDefaultCriteria() =>
    [
        new(CriterionCategory.SoilSuitability, "Soil Suitability", 0.25m, 0m),
        new(CriterionCategory.InfrastructureAccess, "Infrastructure Access", 0.20m, 0m),
        new(CriterionCategory.EnvironmentalCompatibility, "Environmental Compatibility", 0.20m, 0m),
        new(CriterionCategory.ZoningCompliance, "Zoning Compliance", 0.15m, 0m),
        new(CriterionCategory.EconomicPotential, "Economic Potential", 0.10m, 0m),
        new(CriterionCategory.HistoricalPerformance, "Historical Performance", 0.10m, 0m)
    ];
}
