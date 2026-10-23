using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;
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
    private readonly IProjectLeaseReadyDesignationReadRepository _projectLeaseReady;
    private readonly IApprovedPreliminaryLeaseParcelRegistry _approvedPreliminaryRegistry;

    public RuleBasedLandRecommendationEngine(
        ILandParcelRepository landParcelRepository,
        IEnumerable<IRecommendationCriterionEvaluator> evaluators,
        IMlSuitabilityClient mlSuitabilityClient,
        IExperimentalColomboMlEvidenceService experimentalColomboMlEvidence,
        Microsoft.Extensions.Options.IOptions<ExperimentalColomboMlOptions> experimentalOptions,
        IRealParcelSourceReadRepository realParcelSourceReadRepository,
        IRecommendationParcelContext parcelContext,
        IProjectLeaseReadyDesignationReadRepository projectLeaseReady,
        IApprovedPreliminaryLeaseParcelRegistry approvedPreliminaryRegistry)
    {
        _landParcelRepository = landParcelRepository;
        _evaluators = evaluators.OrderBy(e => e.Order).ToList();
        _mlSuitabilityClient = mlSuitabilityClient;
        _experimentalColomboMlEvidence = experimentalColomboMlEvidence;
        _experimentalOptions = experimentalOptions.Value;
        _realParcelSourceReadRepository = realParcelSourceReadRepository;
        _parcelContext = parcelContext;
        _projectLeaseReady = projectLeaseReady;
        _approvedPreliminaryRegistry = approvedPreliminaryRegistry;
    }

    public async Task<LandRecommendationSearchResponse> RecommendAsync(
        LandRecommendationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var (candidates, transparency) = await RetrieveCandidatesWithTransparencyAsync(request, cancellationToken);

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var sources = await _realParcelSourceReadRepository.GetByParcelIdsAsync(
            candidateIds,
            cancellationToken);
        var projectLeaseReady = await _projectLeaseReady.GetByParcelIdsAsync(candidateIds, cancellationToken);
        _parcelContext.Load(sources);

        try
        {
            var drafts = candidates.Select(parcel => BuildDraft(parcel, request)).ToList();

            var rankedDrafts = drafts
                .OrderByDescending(draft => draft.SuitabilityScore)
                .ThenBy(draft => draft.Parcel.Identifier.CadastralNumber, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, request.MaxResults))
                .ToList();

            var rankingAssignments = AssignRankings(drafts, rankedDrafts);

            var recommendations = new List<LandParcelRecommendationResult>(rankedDrafts.Count);
            for (var index = 0; index < rankedDrafts.Count; index++)
            {
                var draft = rankedDrafts[index];
                var ranking = rankingAssignments[index];
                var (mlPrediction, experimentalEvidence) = await CollectSupplementaryEvidenceAsync(
                    draft.Parcel,
                    request.RequiredPurpose,
                    draft.HardConstraint.IsViolated,
                    cancellationToken);

                var source = _parcelContext.GetSource(draft.Parcel.Id);
                var screening = SourceReportedClaimantScreening.Evaluate(source);
                projectLeaseReady.TryGetValue(draft.Parcel.Id, out var designation);
                var leaseReadyEvaluation = designation is null
                    ? null
                    : ProjectLeaseReadyDesignationEvaluator.Evaluate(designation, source);
                var actualLeaseAvailabilityLabel = leaseReadyEvaluation?.DisplayLeaseAvailabilityLabel
                    ?? LandParcelAvailabilityMapper.Build(source, null, false).ActualLeaseAvailabilityLabel;

                recommendations.Add(ToRecommendationResult(
                    draft,
                    request.RequiredPurpose,
                    mlPrediction,
                    experimentalEvidence,
                    ranking,
                    screening,
                    leaseReadyEvaluation,
                    actualLeaseAvailabilityLabel));
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
        RecommendationRankingAssignment ranking,
        StateLandCandidateScreeningResult screening,
        ProjectLeaseReadyDesignationEvaluator.Evaluation? projectLeaseReady,
        string? actualLeaseAvailabilityLabel)
    {
        var evidence = BuildEvidence(
            draft.CriterionResults,
            draft.Restrictions,
            draft.Parcel,
            draft.HardConstraint,
            mlPrediction,
            experimentalEvidence).ToList();

        if (projectLeaseReady?.IsDesignatedInProjectDataset == true)
        {
            evidence.Add(new RecommendationEvidenceDto(
                "ProjectLeaseReadyDesignation",
                $"{projectLeaseReady.ProjectDesignationLabel}. {projectLeaseReady.Designation.DesignationNotice} " +
                $"Official lease availability: {projectLeaseReady.DisplayLeaseAvailabilityLabel}.",
                "Project lease-ready (dataset assumption)"));
        }

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
            ranking.DisplayOrder,
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
            screening.SourceImportedAtUtc,
            projectLeaseReady?.ProjectDesignationLabel,
            projectLeaseReady?.IsDesignatedInProjectDataset ?? false,
            projectLeaseReady?.IsEffectiveForLeaseFlow ?? false,
            draft.Parcel.Area.ToHectares(),
            ranking.DisplayOrder,
            ranking.RuleScoreRank,
            ranking.RuleScoreTieCount,
            ranking.IsTiedOnRuleScore,
            actualLeaseAvailabilityLabel);
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

        var gnDivision = request.PreferredLocation?.GramaNiladhariDivision?.Trim();
        if (!string.IsNullOrWhiteSpace(gnDivision))
        {
            appliedFilters.Add($"gramaNiladhariDivision={gnDivision}");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceReportedLandUse))
        {
            appliedFilters.Add($"sourceReportedLandUse={request.SourceReportedLandUse.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.ResearchLandCoverGroup))
        {
            appliedFilters.Add($"researchLandCoverGroup={request.ResearchLandCoverGroup.Trim()}");
        }

        var targetArea = request.ResolveTargetAreaHectares();
        if (targetArea is > 0)
        {
            appliedFilters.Add($"targetAreaHectares={targetArea.Value} (scoring only, not a minimum filter)");
        }

        // Progressive SQL filters (district, province, source land use).
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

        if (!string.IsNullOrWhiteSpace(gnDivision))
        {
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                District = district,
                GramaNiladhariDivision = gnDivision,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto(
                "Grama Niladhari division",
                count,
                $"Case-insensitive GN division match '{gnDivision}' (parcel field or imported GN mapping)."));
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

        if (!string.IsNullOrWhiteSpace(request.ResearchLandCoverGroup))
        {
            var group = request.ResearchLandCoverGroup.Trim();
            var count = await _landParcelRepository.CountSearchAsync(new LandSearchRequest
            {
                Province = provinceFilter,
                District = district,
                ResearchLandCoverGroup = group,
                Page = 1,
                PageSize = 1
            }, cancellationToken);
            stages.Add(new RecommendationFilterStageDto(
                "Project land-cover group",
                count,
                $"Canonical land use '{group}': original cadastral label and/or qualifying project GIS classification (each parcel counted once)."));
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

        var approvedParcelIds = await _approvedPreliminaryRegistry.GetApprovedParcelIdsAsync(cancellationToken);
        if (approvedParcelIds.Count > 0)
        {
            var beforeApproved = candidates.Count;
            candidates = candidates
                .Where(parcel => !approvedParcelIds.Contains(parcel.Id))
                .ToList();
            var excludedApproved = beforeApproved - candidates.Count;
            appliedFilters.Add("approvedPreliminaryRequest!=current");
            stages.Add(new RecommendationFilterStageDto(
                "Approved preliminary interest",
                candidates.Count,
                excludedApproved == 0
                    ? "Excluded parcels with a current authority-approved preliminary lease interest request (0 excluded). An approved preliminary request is not an executed lease, reserved land, or verified official lease availability."
                    : $"Excluded {excludedApproved} parcel(s) with a current authority-approved preliminary lease interest request ({beforeApproved} → {candidates.Count}). An approved preliminary request is not an executed lease, reserved land, or verified official lease availability."));
        }

        return (candidates, new RecommendationSearchTransparencyDto(
            appliedFilters,
            beforeClaimant,
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
                GramaNiladhariDivision = template.GramaNiladhariDivision,
                CategoryType = template.CategoryType,
                CurrentUseType = template.CurrentUseType,
                SourceReportedLandUse = template.SourceReportedLandUse,
                ResearchLandCoverGroup = template.ResearchLandCoverGroup,
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
            GramaNiladhariDivision = string.IsNullOrWhiteSpace(request.PreferredLocation?.GramaNiladhariDivision)
                ? null
                : request.PreferredLocation.GramaNiladhariDivision.Trim(),
            // Legacy category filter only when explicitly requested (synthetic/domain-managed).
            CategoryType = request.RequiredLandCategory,
            CurrentUseType = request.RequiredLandUse,
            SourceReportedLandUse = string.IsNullOrWhiteSpace(request.SourceReportedLandUse)
                ? null
                : request.SourceReportedLandUse.Trim(),
            ResearchLandCoverGroup = string.IsNullOrWhiteSpace(request.ResearchLandCoverGroup)
                ? null
                : request.ResearchLandCoverGroup.Trim(),
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
                    Source: mlPrediction.ModelSource,
                    Description: mlPrediction.PredictedLabel == "Insufficient Evidence"
                        ? $"Research ML found insufficient evidence ({confidence:P0} model confidence). " +
                          "No ML suitability label was assigned. The rule score and hard constraints are unchanged."
                        : $"ML model predicts '{mlPrediction.PredictedLabel}' suitability " +
                          $"({confidence:P0} model confidence). This is preliminary research evidence, " +
                          "not lease availability or approval, and it does not change rule-based ranking or hard constraints.",
                    RelatedCriterionName: "MlSuitabilityPrediction"));
            }

            if (experimentalEvidence is { Attempted: true })
            {
                evidence.AddRange(experimentalEvidence.Evidence);
            }
        }

        return evidence;
    }

    private sealed record RecommendationRankingAssignment(
        int DisplayOrder,
        int RuleScoreRank,
        int RuleScoreTieCount,
        bool IsTiedOnRuleScore);

    private static IReadOnlyList<RecommendationRankingAssignment> AssignRankings(
        IReadOnlyList<ParcelRecommendationDraft> allDrafts,
        IReadOnlyList<ParcelRecommendationDraft> displayedDrafts)
    {
        var tieCounts = allDrafts
            .GroupBy(d => d.SuitabilityScore)
            .ToDictionary(g => g.Key, g => g.Count());

        var competitionRanks = BuildCompetitionRanks(allDrafts);

        var assignments = new List<RecommendationRankingAssignment>(displayedDrafts.Count);
        for (var index = 0; index < displayedDrafts.Count; index++)
        {
            var draft = displayedDrafts[index];
            var tieCount = tieCounts.TryGetValue(draft.SuitabilityScore, out var count) ? count : 1;
            var ruleScoreRank = competitionRanks.TryGetValue(draft.Parcel.Id, out var rank) ? rank : index + 1;

            assignments.Add(new RecommendationRankingAssignment(
                index + 1,
                ruleScoreRank,
                tieCount,
                tieCount > 1));
        }

        return assignments;
    }

    private static Dictionary<Guid, int> BuildCompetitionRanks(IReadOnlyList<ParcelRecommendationDraft> drafts)
    {
        var ordered = drafts
            .OrderByDescending(d => d.SuitabilityScore)
            .ThenBy(d => d.Parcel.Identifier.CadastralNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ranks = new Dictionary<Guid, int>(ordered.Count);
        var rank = 0;
        decimal? previousScore = null;

        foreach (var draft in ordered)
        {
            if (previousScore is null || draft.SuitabilityScore != previousScore.Value)
            {
                rank++;
                previousScore = draft.SuitabilityScore;
            }

            ranks[draft.Parcel.Id] = rank;
        }

        return ranks;
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
