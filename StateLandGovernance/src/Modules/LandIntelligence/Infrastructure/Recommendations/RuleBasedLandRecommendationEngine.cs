using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

public sealed class RuleBasedLandRecommendationEngine : ILandRecommendationEngine
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly ISpatialAnalysisService _spatialAnalysisService;
    private readonly IEnumerable<IRecommendationCriterionEvaluator> _evaluators;

    public RuleBasedLandRecommendationEngine(
        ILandParcelRepository landParcelRepository,
        ISpatialAnalysisService spatialAnalysisService,
        IEnumerable<IRecommendationCriterionEvaluator> evaluators)
    {
        _landParcelRepository = landParcelRepository;
        _spatialAnalysisService = spatialAnalysisService;
        _evaluators = evaluators.OrderBy(e => e.Order).ToList();
    }

    public async Task<LandRecommendationSearchResponse> RecommendAsync(
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var candidates = await RetrieveCandidatesAsync(request, cancellationToken);
        var evaluations = new List<LandParcelRecommendationResult>();

        foreach (var parcel in candidates)
        {
            var criterionResults = EvaluateParcel(parcel, request);
            var matching = criterionResults.Where(c => c.IsMet).ToList();
            var failed = criterionResults.Where(c => !c.IsMet).ToList();
            var restrictions = ParcelRestrictionCollector.Collect(parcel);
            var score = RecommendationScoreCalculator.Calculate(criterionResults);
            var evidence = BuildEvidence(criterionResults, restrictions);
            var explanation = RecommendationExplanationBuilder.Build(
                parcel.Identifier.CadastralNumber,
                request.RequiredPurpose,
                score,
                matching,
                failed,
                restrictions);

            evaluations.Add(new LandParcelRecommendationResult(
                parcel.Id,
                parcel.Identifier.CadastralNumber,
                score,
                Rank: 0,
                matching,
                failed,
                restrictions,
                evidence,
                explanation));
        }

        var ranked = evaluations
            .OrderByDescending(r => r.SuitabilityScore)
            .ThenBy(r => r.CadastralNumber, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, request.MaxResults))
            .Select((result, index) => result with { Rank = index + 1 })
            .ToList();

        return new LandRecommendationSearchResponse(
            request.RequiredPurpose,
            ranked,
            candidates.Count,
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<LandParcel>> RetrieveCandidatesAsync(
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetParcelId is not null)
        {
            var parcel = await _landParcelRepository.GetByIdAsync(request.TargetParcelId.Value, cancellationToken);
            return parcel is null ? [] : [parcel];
        }

        var searchRequest = BuildSearchRequest(request);
        var candidates = await _landParcelRepository.SearchAsync(searchRequest, cancellationToken);

        if (request.PreferredLocation?.Province is null
            && request.PreferredLocation?.District is null
            && request.Accessibility?.MaxRoadDistanceMeters is null)
        {
            return candidates;
        }

        return await ApplySpatialFilteringAsync(candidates, request, cancellationToken);
    }

    private static LandSearchRequest BuildSearchRequest(LandRecommendationSearchRequest request) =>
        new()
        {
            Province = request.PreferredLocation?.Province,
            District = request.PreferredLocation?.District,
            DivisionalSecretariat = request.PreferredLocation?.DivisionalSecretariat,
            CategoryType = request.RequiredLandCategory,
            CurrentUseType = request.RequiredLandUse,
            MinArea = request.RequiredAreaHectares is > 0
                ? request.RequiredAreaHectares * (1m - request.AreaTolerancePercent / 100m)
                : null,
            Page = 1,
            PageSize = Math.Max(request.MaxResults * 5, 50)
        };

    private async Task<IReadOnlyList<LandParcel>> ApplySpatialFilteringAsync(
        IReadOnlyList<LandParcel> candidates,
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Accessibility?.MaxRoadDistanceMeters is not > 0)
        {
            return candidates;
        }

        var filteredIds = new HashSet<Guid>();
        foreach (var parcel in candidates)
        {
            foreach (var feature in parcel.InfrastructureFeatures.Where(f => f.DistanceMeters.HasValue))
            {
                var distance = await _spatialAnalysisService.CalculateDistanceBetweenParcelAndInfrastructureAsync(
                    parcel.Id,
                    feature.Id,
                    cancellationToken);

                if (distance.DistanceMeters <= (double)request.Accessibility.MaxRoadDistanceMeters.Value)
                {
                    filteredIds.Add(parcel.Id);
                    break;
                }
            }

            if (parcel.InfrastructureFeatures.Any(f =>
                    f.DistanceMeters <= request.Accessibility.MaxRoadDistanceMeters))
            {
                filteredIds.Add(parcel.Id);
            }
        }

        return candidates.Where(c => filteredIds.Contains(c.Id)).ToList();
    }

    private IReadOnlyList<CriterionEvaluationDto> EvaluateParcel(
        LandParcel parcel,
        LandRecommendationSearchRequest request) =>
        _evaluators
            .Where(e => e.IsApplicable(request))
            .Select(e => e.Evaluate(parcel, request))
            .ToList();

    private static IReadOnlyList<RecommendationEvidenceDto> BuildEvidence(
        IReadOnlyList<CriterionEvaluationDto> evaluations,
        IReadOnlyList<RestrictionSummaryDto> restrictions)
    {
        var evidence = evaluations
            .Select(e => new RecommendationEvidenceDto(
                "RuleBasedCriterionEvaluator",
                e.Summary,
                e.Name))
            .ToList();

        evidence.AddRange(restrictions.Select(r => new RecommendationEvidenceDto(
            r.Source,
            r.Description,
            r.RestrictionType)));

        return evidence;
    }
}
