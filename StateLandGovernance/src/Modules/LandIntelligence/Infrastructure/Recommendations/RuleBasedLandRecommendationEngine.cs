using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

public sealed class RuleBasedLandRecommendationEngine : ILandRecommendationEngine
{
    private const int CandidatePageSize = 100;
    private const int MaxCandidates = 2000;

    private readonly ILandParcelRepository _landParcelRepository;
    private readonly IEnumerable<IRecommendationCriterionEvaluator> _evaluators;
    private readonly IMlSuitabilityClient _mlSuitabilityClient;
    private readonly IExperimentalColomboMlEvidenceService _experimentalColomboMlEvidence;
    private readonly ExperimentalColomboMlOptions _experimentalOptions;
    private readonly IRealParcelSourceReadRepository _realParcelSourceReadRepository;
    private readonly IRecommendationParcelContext _parcelContext;

    public RuleBasedLandRecommendationEngine(
        ILandParcelRepository landParcelRepository,
        IEnumerable<IRecommendationCriterionEvaluator> evaluators,
        IMlSuitabilityClient mlSuitabilityClient,
        IExperimentalColomboMlEvidenceService experimentalColomboMlEvidence,
        Microsoft.Extensions.Options.IOptions<ExperimentalColomboMlOptions> experimentalOptions,
        IRealParcelSourceReadRepository realParcelSourceReadRepository,
        IRecommendationParcelContext parcelContext)
    {
        _landParcelRepository = landParcelRepository;
        _evaluators = evaluators.OrderBy(e => e.Order).ToList();
        _mlSuitabilityClient = mlSuitabilityClient;
        _experimentalColomboMlEvidence = experimentalColomboMlEvidence;
        _experimentalOptions = experimentalOptions.Value;
        _realParcelSourceReadRepository = realParcelSourceReadRepository;
        _parcelContext = parcelContext;
    }

    public async Task<LandRecommendationSearchResponse> RecommendAsync(
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var (candidates, transparency) = await RetrieveCandidatesWithTransparencyAsync(request, cancellationToken);

        var sources = await _realParcelSourceReadRepository.GetByParcelIdsAsync(
            candidates.Select(c => c.Id).ToList(),
            cancellationToken);
        _parcelContext.Load(sources);

        try
        {
            var drafts = candidates.Select(parcel => BuildDraft(parcel, request)).ToList();

            var rankedDrafts = drafts
                .OrderByDescending(draft => draft.SuitabilityScore)
                .ThenBy(draft => draft.Parcel.Identifier.CadastralNumber, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, request.MaxResults))
                .ToList();

            var recommendations = new List<LandParcelRecommendationResult>(rankedDrafts.Count);
            for (var index = 0; index < rankedDrafts.Count; index++)
            {
                var draft = rankedDrafts[index];
                var (mlPrediction, experimentalEvidence) = await CollectSupplementaryEvidenceAsync(
                    draft.Parcel,
                    request.RequiredPurpose,
                    draft.HardConstraint.IsViolated,
                    cancellationToken);

                recommendations.Add(ToRecommendationResult(
                    draft,
                    request.RequiredPurpose,
                    mlPrediction,
                    experimentalEvidence,
                    index + 1,
                    SourceReportedClaimantScreening.Evaluate(_parcelContext.GetSource(draft.Parcel.Id))));
            }

            var unavailable = CollectUnavailableCriteria(request, drafts);
            var finalTransparency = transparency with
            {
                UnavailableCriteria = unavailable
            };

            return new LandRecommendationSearchResponse(
                request.RequiredPurpose,
                recommendations,
                candidates.Count,
                DateTimeOffset.UtcNow,
                finalTransparency);
        }
        finally
        {
            _parcelContext.Clear();
        }
    }

    private ParcelRecommendationDraft BuildDraft(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var criterionResults = EvaluateParcel(parcel, request);
        var matching = criterionResults.Where(c => c.IsMet).ToList();
        var failed = criterionResults.Where(c => !c.IsMet).ToList();
        var restrictions = ParcelRestrictionCollector.Collect(parcel);
        var hardConstraint = HardConstraintEvaluator.Evaluate(parcel);
        var score = hardConstraint.IsViolated
            ? 0m
            : RecommendationScoreCalculator.Calculate(criterionResults);
        var gisSupplementarySummaries = GisDerivedRecommendationEvidenceCollector.CollectSupplementarySummaries(parcel);

        return new ParcelRecommendationDraft(
            parcel,
            criterionResults,
            matching,
            failed,
            restrictions,
            hardConstraint,
            score,
            gisSupplementarySummaries);
    }

    private async Task<(MlSuitabilityPrediction? MlPrediction, ExperimentalColomboMlEvidenceResult? ExperimentalEvidence)>
        CollectSupplementaryEvidenceAsync(
            LandParcel parcel,
            LandUseType requiredPurpose,
            bool hardConstraintViolated,
            CancellationToken cancellationToken)
    {
        if (hardConstraintViolated)
        {
            return (null, null);
        }

        if (_experimentalOptions.Enabled)
        {
            var experimentalEvidence = await _experimentalColomboMlEvidence.CollectAsync(
                parcel,
                requiredPurpose,
                cancellationToken);

            if (_experimentalOptions.SuppressProductionMlWhenEnabled)
            {
                return (null, experimentalEvidence);
            }

            var mlPrediction = await _mlSuitabilityClient.PredictAsync(
                parcel,
                requiredPurpose,
                cancellationToken);
            return (mlPrediction, experimentalEvidence);
        }

        var productionMl = await _mlSuitabilityClient.PredictAsync(
            parcel,
            requiredPurpose,
            cancellationToken);
        return (productionMl, null);
    }

    private static LandParcelRecommendationResult ToRecommendationResult(
        ParcelRecommendationDraft draft,
        LandUseType requiredPurpose,
        MlSuitabilityPrediction? mlPrediction,
        ExperimentalColomboMlEvidenceResult? experimentalEvidence,
        int rank,
        StateLandCandidateScreeningResult screening)
    {
        var evidence = BuildEvidence(
            draft.CriterionResults,
            draft.Restrictions,
            draft.Parcel,
            draft.HardConstraint,
            mlPrediction,
            experimentalEvidence);

        var explanation = draft.HardConstraint.IsViolated
            ? RecommendationExplanationBuilder.BuildHardConstraintRejection(
                draft.Parcel.Identifier.CadastralNumber,
                requiredPurpose,
                draft.HardConstraint.Summary,
                draft.Restrictions)
            : RecommendationExplanationBuilder.Build(
                draft.Parcel.Identifier.CadastralNumber,
                requiredPurpose,
                draft.SuitabilityScore,
                draft.MatchingCriteria,
                draft.FailedCriteria,
                draft.Restrictions,
                draft.GisSupplementarySummaries);

        return new LandParcelRecommendationResult(
            draft.Parcel.Id,
            draft.Parcel.Identifier.CadastralNumber,
            draft.SuitabilityScore,
            rank,
            draft.MatchingCriteria,
            draft.FailedCriteria,
            draft.Restrictions,
            evidence,
            explanation,
            draft.HardConstraint.IsViolated,
            draft.HardConstraint.IsViolated ? draft.HardConstraint.Summary : null,
            screening.StatusLabel,
            screening.Reason,
            screening.ClaimantCategoryCode,
            screening.ClaimantCategoryDisplay,
            screening.ParcelStatusCode,
            screening.SourceProvenanceLabel,
            screening.SourceImportedAtUtc);
    }

    private async Task<(IReadOnlyList<LandParcel> Candidates, RecommendationSearchTransparencyDto Transparency)>
        RetrieveCandidatesWithTransparencyAsync(
            LandRecommendationSearchRequest request,
            CancellationToken cancellationToken)
    {
        var appliedFilters = new List<string>();
        var stages = new List<RecommendationFilterStageDto>();

        if (request.TargetParcelId is not null)
        {
            var parcel = await _landParcelRepository.GetByIdAsync(request.TargetParcelId.Value, cancellationToken);
            var list = parcel is null ? Array.Empty<LandParcel>() : new[] { parcel };
            appliedFilters.Add($"targetParcelId={request.TargetParcelId}");
            stages.Add(new RecommendationFilterStageDto("Target parcel", list.Length, "Single-parcel evaluation."));

            if (list.Length > 0)
            {
                var targetSources = await _realParcelSourceReadRepository.GetByParcelIdsAsync(
                    [list[0].Id],
                    cancellationToken);
                targetSources.TryGetValue(list[0].Id, out var targetSource);
                var screening = SourceReportedClaimantScreening.Evaluate(targetSource);
                appliedFilters.Add("sourceReportedClaimantCategory!=Private");
                if (!screening.IncludeInCandidateResults)
                {
                    stages.Add(new RecommendationFilterStageDto(
                        "State-land claimant screening",
                        0,
                        "Target parcel excluded: source-reported claimant category is Private."));
                    list = Array.Empty<LandParcel>();
                }
                else
                {
                    stages.Add(new RecommendationFilterStageDto(
                        "State-land claimant screening",
                        list.Length,
                        screening.StatusLabel));
                }
            }

            return (list, new RecommendationSearchTransparencyDto(
                appliedFilters,
                parcel is null ? 0 : 1,
                list.Length,
                stages,
                []));
        }

        var beforeCount = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
        {
            Page = 1,
            PageSize = 1
        }, cancellationToken);
        stages.Add(new RecommendationFilterStageDto(
            "Indexed parcels (DEMO-* excluded)",
            beforeCount,
            "All searchable parcels before recommendation filters."));

        var district = request.PreferredLocation?.District?.Trim();
        if (!string.IsNullOrWhiteSpace(district))
        {
            appliedFilters.Add($"district={district}");
        }
        else
        {
            appliedFilters.Add("district=(all)");
        }

        if (!string.IsNullOrWhiteSpace(request.PreferredLocation?.Province))
        {
            appliedFilters.Add($"province={request.PreferredLocation.Province.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceReportedLandUse))
        {
            appliedFilters.Add($"sourceReportedLandUse={request.SourceReportedLandUse.Trim()}");
        }

        if (request.RequiredAreaHectares is > 0)
        {
            appliedFilters.Add($"minAreaHectares={request.RequiredAreaHectares.Value}");
        }

        // Progressive SQL filters (district, province, source land use, area).
        // Min area is a hard floor — no silent tolerance in candidate selection.
        var searchRequest = BuildSearchRequest(request);
        var candidates = await LoadAllMatchingAsync(searchRequest, cancellationToken);

        string? provinceFilter = string.IsNullOrWhiteSpace(request.PreferredLocation?.Province)
            ? null
            : request.PreferredLocation.Province.Trim();
        string? sourceLandUseFilter = string.IsNullOrWhiteSpace(request.SourceReportedLandUse)
            ? null
            : request.SourceReportedLandUse.Trim();

        if (provinceFilter is not null)
        {
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto("Province", count, $"Province filter '{provinceFilter}'."));
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                District = district,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto(
                "District",
                count,
                $"Case-insensitive district match '{district}'. Unsupported districts are not remapped."));
        }

        if (sourceLandUseFilter is not null)
        {
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                District = district,
                SourceReportedLandUse = sourceLandUseFilter,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto(
                "Source-reported land use",
                count,
                $"Exact match on '{sourceLandUseFilter}'."));
        }

        if (request.RequiredAreaHectares is > 0)
        {
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                District = district,
                SourceReportedLandUse = sourceLandUseFilter,
                MinArea = request.RequiredAreaHectares,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto(
                "Minimum area",
                count,
                $"Excluded parcels below {request.RequiredAreaHectares.Value:F2} ha."));
        }

        // Source-reported Private claimant category is excluded from state-land candidates.
        // This is screening only — not ownership or lease-availability confirmation.
        var sourceByParcelId = await _realParcelSourceReadRepository.GetByParcelIdsAsync(
            candidates.Select(c => c.Id).ToList(),
            cancellationToken);
        var beforeClaimant = candidates.Count;
        var retained = new List<LandParcel>(candidates.Count);
        var excludedPrivate = 0;
        foreach (var parcel in candidates)
        {
            sourceByParcelId.TryGetValue(parcel.Id, out var source);
            var screening = SourceReportedClaimantScreening.Evaluate(source);
            if (!screening.IncludeInCandidateResults)
            {
                excludedPrivate++;
                continue;
            }

            retained.Add(parcel);
        }

        candidates = retained;
        appliedFilters.Add("sourceReportedClaimantCategory!=Private");
        stages.Add(new RecommendationFilterStageDto(
            "State-land claimant screening",
            candidates.Count,
            excludedPrivate == 0
                ? "Excluded source-reported Private claimant parcels from state-land candidates (0 excluded). Remaining parcels still require official availability verification."
                : $"Excluded {excludedPrivate} source-reported Private parcel(s) from state-land candidates ({beforeClaimant} → {candidates.Count}). Remaining parcels still require official availability verification."));

        return (candidates, new RecommendationSearchTransparencyDto(
            appliedFilters,
            beforeCount,
            candidates.Count,
            stages,
            []));
    }

    private async Task<IReadOnlyList<LandParcel>> LoadAllMatchingAsync(
        LandSearchRequest template,
        CancellationToken cancellationToken)
    {
        var combined = new List<LandParcel>();
        var page = 1;
        while (combined.Count < MaxCandidates)
        {
            var batch = await _landParcelRepository.SearchAsync(new LandSearchRequest
            {
                Province = template.Province,
                District = template.District,
                DivisionalSecretariat = template.DivisionalSecretariat,
                CategoryType = template.CategoryType,
                CurrentUseType = template.CurrentUseType,
                SourceReportedLandUse = template.SourceReportedLandUse,
                MinArea = template.MinArea,
                MaxArea = template.MaxArea,
                MinLatitude = template.MinLatitude,
                MaxLatitude = template.MaxLatitude,
                MinLongitude = template.MinLongitude,
                MaxLongitude = template.MaxLongitude,
                Page = page,
                PageSize = CandidatePageSize
            }, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            combined.AddRange(batch);
            if (batch.Count < CandidatePageSize)
            {
                break;
            }

            page++;
        }

        return combined.Count > MaxCandidates
            ? combined.Take(MaxCandidates).ToList()
            : combined;
    }

    private static LandSearchRequest BuildSearchRequest(LandRecommendationSearchRequest request) =>
        new()
        {
            Province = string.IsNullOrWhiteSpace(request.PreferredLocation?.Province)
                ? null
                : request.PreferredLocation.Province.Trim(),
            District = string.IsNullOrWhiteSpace(request.PreferredLocation?.District)
                ? null
                : request.PreferredLocation.District.Trim(),
            DivisionalSecretariat = string.IsNullOrWhiteSpace(request.PreferredLocation?.DivisionalSecretariat)
                ? null
                : request.PreferredLocation.DivisionalSecretariat.Trim(),
            // Legacy category filter only when explicitly requested (synthetic/domain-managed).
            CategoryType = request.RequiredLandCategory,
            CurrentUseType = request.RequiredLandUse,
            SourceReportedLandUse = string.IsNullOrWhiteSpace(request.SourceReportedLandUse)
                ? null
                : request.SourceReportedLandUse.Trim(),
            // Hard minimum area — do not soften with AreaTolerancePercent at filter time.
            MinArea = request.RequiredAreaHectares is > 0 ? request.RequiredAreaHectares : null,
            Page = 1,
            PageSize = CandidatePageSize
        };

    private IReadOnlyList<CriterionEvaluationDto> EvaluateParcel(
        LandParcel parcel,
        LandRecommendationSearchRequest request) =>
        _evaluators
            .Where(e => e.IsApplicable(request))
            .Select(e => e.Evaluate(parcel, request))
            .ToList();

    private static IReadOnlyList<RecommendationUnavailableCriterionDto> CollectUnavailableCriteria(
        LandRecommendationSearchRequest request,
        IReadOnlyList<ParcelRecommendationDraft> drafts)
    {
        _ = request;
        return drafts
            .SelectMany(d => d.CriterionResults)
            .Where(c => !c.WasAssessed)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new RecommendationUnavailableCriterionDto(
                g.Key,
                g.First().NotAssessedReason ?? g.First().Summary))
            .ToList();
    }

    private static IReadOnlyList<RecommendationEvidenceDto> BuildEvidence(
        IReadOnlyList<CriterionEvaluationDto> evaluations,
        IReadOnlyList<RestrictionSummaryDto> restrictions,
        LandParcel parcel,
        HardConstraintEvaluation hardConstraint,
        MlSuitabilityPrediction? mlPrediction,
        ExperimentalColomboMlEvidenceResult? experimentalEvidence)
    {
        var evidence = evaluations
            .Select(e => new RecommendationEvidenceDto(
                e.DataProvenance is not null
                    ? ProvenanceEvidenceFormatter.DescribeSource(e.DataProvenance)
                    : "RuleBasedCriterionEvaluator",
                e.WasAssessed
                    ? ProvenanceEvidenceFormatter.AppendProvenance(e.Summary, e.DataProvenance)
                    : $"{e.Summary} (criterion not assessed)",
                e.Name,
                e.DataProvenance))
            .ToList();

        evidence.AddRange(restrictions.Select(r => new RecommendationEvidenceDto(
            r.DataProvenance is not null
                ? ProvenanceEvidenceFormatter.DescribeSource(r.DataProvenance)
                : r.Source,
            ProvenanceEvidenceFormatter.AppendProvenance(r.Description, r.DataProvenance),
            r.RestrictionType,
            r.DataProvenance)));

        evidence.AddRange(GisDerivedRecommendationEvidenceCollector.CollectEvidence(parcel));

        if (hardConstraint.IsViolated)
        {
            evidence.Add(new RecommendationEvidenceDto(
                Source: "HardConstraintEvaluator",
                Description: "Parcel rejected due to hard legal or environmental restrictions: "
                             + hardConstraint.Summary
                             + " ML suitability prediction was not applied.",
                RelatedCriterionName: "HardConstraintRejection"));
        }
        else
        {
            if (mlPrediction is not null)
            {
                var confidence = mlPrediction.Probabilities.TryGetValue(mlPrediction.PredictedLabel, out var p)
                    ? p
                    : 0m;

                evidence.Add(new RecommendationEvidenceDto(
                    Source: "RandomForestSuitabilityModel",
                    Description: $"ML model predicts '{mlPrediction.PredictedLabel}' suitability " +
                                 $"({confidence:P0} confidence). This is supplementary evidence and does " +
                                 "not override the rule-based hard constraints above.",
                    RelatedCriterionName: "MlSuitabilityPrediction"));
            }

            if (experimentalEvidence is { Attempted: true })
            {
                evidence.AddRange(experimentalEvidence.Evidence);
            }
        }

        return evidence;
    }

    private sealed record ParcelRecommendationDraft(
        LandParcel Parcel,
        IReadOnlyList<CriterionEvaluationDto> CriterionResults,
        IReadOnlyList<CriterionEvaluationDto> MatchingCriteria,
        IReadOnlyList<CriterionEvaluationDto> FailedCriteria,
        IReadOnlyList<RestrictionSummaryDto> Restrictions,
        HardConstraintEvaluation HardConstraint,
        decimal SuitabilityScore,
        IReadOnlyList<string> GisSupplementarySummaries);
}
