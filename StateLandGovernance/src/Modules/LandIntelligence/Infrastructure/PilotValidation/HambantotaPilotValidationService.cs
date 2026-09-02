using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
namespace StateLandGovernance.LandIntelligence.Infrastructure.PilotValidation;

internal sealed class HambantotaPilotValidationService : IHambantotaPilotValidationService
{
    private readonly ILandParcelRepository _parcelRepository;
    private readonly ILandParcelGisEnrichmentService _enrichmentService;
    private readonly ILandParcelGisEnrichmentPersistenceService _persistenceService;
    private readonly ILandParcelGisKnowledgeGraphSyncService _graphSyncService;
    private readonly ILandRecommendationEngine _recommendationEngine;
    private readonly IKnowledgeGraphService _knowledgeGraphService;
    private readonly LandIntelligenceDbContext _dbContext;

    public HambantotaPilotValidationService(
        ILandParcelRepository parcelRepository,
        ILandParcelGisEnrichmentService enrichmentService,
        ILandParcelGisEnrichmentPersistenceService persistenceService,
        ILandParcelGisKnowledgeGraphSyncService graphSyncService,
        ILandRecommendationEngine recommendationEngine,
        IKnowledgeGraphService knowledgeGraphService,
        LandIntelligenceDbContext dbContext)
    {
        _parcelRepository = parcelRepository;
        _enrichmentService = enrichmentService;
        _persistenceService = persistenceService;
        _graphSyncService = graphSyncService;
        _recommendationEngine = recommendationEngine;
        _knowledgeGraphService = knowledgeGraphService;
        _dbContext = dbContext;
    }

    public async Task<HambantotaPilotValidationReport> ValidateScenariosAsync(
        IReadOnlyList<HambantotaPilotValidationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var scenarios = new List<HambantotaPilotValidationScenarioResult>(requests.Count);

        foreach (var request in requests)
        {
            scenarios.Add(await ValidateScenarioAsync(request, cancellationToken));
        }

        return BuildReport(scenarios);
    }

    public async Task<HambantotaPilotValidationScenarioResult> ValidateScenarioAsync(
        HambantotaPilotValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcelBefore = await _parcelRepository.GetByIdAsync(request.ParcelId)
            ?? throw new InvalidOperationException($"Parcel {request.ParcelId} was not found for H13 validation.");

        var officialSoilType = parcelBefore.Characteristics?.SoilType;
        var officialProvince = parcelBefore.Location.Province;
        var officialDistrict = parcelBefore.Location.District;

        var firstEnrichment = await _enrichmentService.EnrichAsync(request.ParcelId, cancellationToken);
        var secondEnrichment = await _enrichmentService.EnrichAsync(request.ParcelId, cancellationToken);

        await _persistenceService.PersistAsync(firstEnrichment, cancellationToken);
        await _persistenceService.PersistAsync(secondEnrichment, cancellationToken);

        var idempotentPersistence = await VerifyPersistenceIdempotencyAsync(request.ParcelId, cancellationToken);

        HambantotaPilotValidationKnowledgeGraphResult? knowledgeGraph = null;
        var idempotentGraphSync = true;

        if (request.SyncToKnowledgeGraph && Neo4jSettings.IsConfigured())
        {
            await _graphSyncService.SyncAsync(request.ParcelId, cancellationToken);
            await _graphSyncService.SyncAsync(request.ParcelId, cancellationToken);

            knowledgeGraph = await BuildKnowledgeGraphResultAsync(request.ParcelId, cancellationToken);
            idempotentGraphSync = await VerifyGraphSyncIdempotencyAsync(request.ParcelId, cancellationToken);
        }
        else if (request.SyncToKnowledgeGraph)
        {
            knowledgeGraph = new HambantotaPilotValidationKnowledgeGraphResult
            {
                Status = "Skipped",
                Notes = "Neo4j is not configured in the current environment."
            };
        }

        var parcelAfter = await _parcelRepository.GetByIdAsync(request.ParcelId)
            ?? throw new InvalidOperationException($"Parcel {request.ParcelId} was not found after H13 pipeline.");

        var recommendations = new List<HambantotaPilotValidationRecommendationResult>(request.LandUsePurposes.Count);

        foreach (var purpose in request.LandUsePurposes)
        {
            var response = await _recommendationEngine.RecommendAsync(
                BuildRecommendationRequest(request, purpose),
                cancellationToken);

            var recommendation = response.Recommendations.FirstOrDefault(item => item.ParcelId == request.ParcelId);
            recommendations.Add(new HambantotaPilotValidationRecommendationResult
            {
                RequestedPurpose = purpose,
                FinalRank = recommendation?.Rank,
                SuitabilityScore = recommendation?.SuitabilityScore,
                IsRecommended = recommendation is not null,
                MatchingCriteria = recommendation?.MatchingCriteria ?? [],
                FailedCriteria = recommendation?.FailedCriteria ?? [],
                Restrictions = recommendation?.Restrictions ?? [],
                Evidence = recommendation?.Evidence ?? [],
                Explanation = recommendation?.Explanation
            });
        }

        var behaviourChecks = BuildBehaviourChecks(
            parcelBefore,
            parcelAfter,
            firstEnrichment,
            secondEnrichment,
            idempotentPersistence,
            idempotentGraphSync);

        var metrics = BuildMetrics(request, parcelAfter, firstEnrichment, officialSoilType);

        return new HambantotaPilotValidationScenarioResult
        {
            Metrics = metrics,
            Recommendations = recommendations,
            KnowledgeGraph = knowledgeGraph,
            BehaviourChecks = behaviourChecks
        };
    }

    private static LandRecommendationSearchRequest BuildRecommendationRequest(
        HambantotaPilotValidationRequest request,
        LandUseType purpose) =>
        new()
        {
            TargetParcelId = request.ParcelId,
            RequiredPurpose = purpose,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = purpose,
            RequiredAreaHectares = 0.5m,
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = request.RequireRoadAccess,
                MaxRoadDistanceMeters = request.MaxRoadDistanceMeters
            },
            Environmental = new EnvironmentalCriteria
            {
                MaxAllowedEnvironmentalSeverity = RestrictionSeverity.Medium,
                RejectProhibitiveEnvironmentalRestrictions = true
            },
            MaxResults = 5
        };

    private static HambantotaPilotValidationMetrics BuildMetrics(
        HambantotaPilotValidationRequest request,
        LandParcel parcel,
        LandParcelGisEnrichmentResult enrichment,
        string? officialSoilType)
    {
        var gisRoad = parcel.InfrastructureFeatures
            .FirstOrDefault(feature =>
                feature.Type == InfrastructureFeatureType.Road
                && feature.DistanceProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName);

        var gisWater = parcel.InfrastructureFeatures
            .FirstOrDefault(feature =>
                feature.Type == InfrastructureFeatureType.Other
                && feature.DistanceProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName);

        var conservationRestrictions = parcel.EnvironmentalRestrictions
            .Where(restriction =>
                restriction.DataProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName)
            .ToList();

        var provenanceSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (enrichment.Administrative?.SourceName is { } adminSource)
        {
            provenanceSources.Add(adminSource);
        }

        if (enrichment.RoadAccessibility?.SourceName is { } roadSource)
        {
            provenanceSources.Add(roadSource);
        }

        if (enrichment.WaterProximity?.SourceName is { } waterSource)
        {
            provenanceSources.Add(waterSource);
        }

        if (enrichment.Soil?.SourceName is { } soilSource)
        {
            provenanceSources.Add(soilSource);
        }

        if (enrichment.Environmental?.SourceName is { } envSource)
        {
            provenanceSources.Add(envSource);
        }

        provenanceSources.Add(GisDerivedIntelligenceOwnership.SourceName);

        return new HambantotaPilotValidationMetrics
        {
            ParcelId = parcel.Id,
            CadastralNumber = parcel.Identifier.CadastralNumber,
            ScenarioKey = request.ScenarioKey,
            ScenarioDescription = request.ScenarioDescription,
            UsesSyntheticGisData = request.UsesSyntheticGisData,
            GisEnrichmentOverallStatus = enrichment.OverallStatus.ToString(),
            DetectedProvince = enrichment.Administrative?.DetectedProvince,
            DetectedDistrict = enrichment.Administrative?.DetectedDistrict,
            MappedRoadDistanceMeters = gisRoad?.DistanceMeters
                ?? (enrichment.RoadAccessibility?.DistanceMeters.HasValue == true
                    ? (decimal?)enrichment.RoadAccessibility.DistanceMeters.Value
                    : null),
            MappedRoadName = gisRoad?.Name ?? enrichment.RoadAccessibility?.RoadName,
            MappedWaterDistanceMeters = gisWater?.DistanceMeters
                ?? (enrichment.WaterProximity?.DistanceMeters.HasValue == true
                    ? (decimal?)enrichment.WaterProximity.DistanceMeters.Value
                    : null),
            MappedWaterFeatureType = enrichment.WaterProximity?.FeatureType?.ToString(),
            OfficialSoilType = officialSoilType,
            GisDerivedSoilGroup = parcel.GisDerivedIntelligence?.DerivedSoilGroup?.SoilGroupName
                ?? enrichment.Soil?.PrimarySoilGroup,
            ConservationIntersection = enrichment.Environmental?.IntersectsSoilConservationArea,
            ConservationAreaCount = conservationRestrictions.Count > 0
                ? conservationRestrictions.Count
                : enrichment.Environmental?.ConservationAreas.Count,
            ErosionDataAvailability = enrichment.Environmental?.ErosionDataStatus.ToString()
                ?? ErosionDataStatus.Unavailable.ToString(),
            EnrichmentEvidence = enrichment.Evidence,
            EnrichmentWarnings = enrichment.Warnings,
            EnrichmentFailures = enrichment.Failures
                .Select(failure => $"{failure.Section}: {failure.Message}")
                .ToList(),
            ProvenanceSources = provenanceSources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static HambantotaPilotValidationBehaviourChecks BuildBehaviourChecks(
        LandParcel parcelBefore,
        LandParcel parcelAfter,
        LandParcelGisEnrichmentResult firstEnrichment,
        LandParcelGisEnrichmentResult secondEnrichment,
        bool idempotentPersistence,
        bool idempotentGraphSync)
    {
        var gisWaterFeatures = parcelAfter.InfrastructureFeatures
            .Where(feature =>
                feature.Type == InfrastructureFeatureType.Other
                && feature.DistanceProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName)
            .ToList();

        var gisConservationRestrictions = parcelAfter.EnvironmentalRestrictions
            .Where(restriction =>
                restriction.DataProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName)
            .ToList();

        var conservationIntersectionCount = firstEnrichment.Environmental?.ConservationAreas.Count ?? 0;
        var conservationNotDoubleCounted = gisConservationRestrictions.Count == 0
            || gisConservationRestrictions.Count == conservationIntersectionCount;

        var officialSoil = parcelBefore.Characteristics?.SoilType;
        var gisSoil = parcelAfter.GisDerivedIntelligence?.DerivedSoilGroup?.SoilGroupName
            ?? firstEnrichment.Soil?.PrimarySoilGroup;

        var gisSoilSeparate = officialSoil is null
            || gisSoil is null
            || !string.Equals(officialSoil, gisSoil, StringComparison.OrdinalIgnoreCase);

        var erosionEvidence = firstEnrichment.Environmental is null
            ? []
            : firstEnrichment.Evidence
                .Concat(firstEnrichment.Environmental.Evidence)
                .ToList();

        var missingErosionNotSafety = firstEnrichment.Environmental?.ErosionDataStatus != ErosionDataStatus.Unavailable
            || !erosionEvidence.Any(item =>
                item.Contains("low risk", StringComparison.OrdinalIgnoreCase)
                || item.Contains("no erosion", StringComparison.OrdinalIgnoreCase));

        var noFabricatedEvidence = firstEnrichment.OverallStatus != LandParcelGisEnrichmentOverallStatus.Unavailable
            || (firstEnrichment.RoadAccessibility?.RoadId is null
                && firstEnrichment.WaterProximity?.FeatureId is null
                && firstEnrichment.Soil?.PrimarySoilGroupId is null
                && firstEnrichment.Environmental?.ConservationAreas.Count == 0);

        return new HambantotaPilotValidationBehaviourChecks
        {
            OfficialSoilTypeUnchanged = parcelBefore.Characteristics?.SoilType == parcelAfter.Characteristics?.SoilType,
            OfficialProvinceUnchanged = parcelBefore.Location.Province == parcelAfter.Location.Province,
            OfficialDistrictUnchanged = parcelBefore.Location.District == parcelAfter.Location.District,
            NaturalWaterNotTreatedAsWaterSupply = gisWaterFeatures.All(feature =>
                !string.Equals(feature.Name, "WaterSupply", StringComparison.OrdinalIgnoreCase)),
            GisSoilSeparateFromOfficialSoil = gisSoilSeparate,
            ConservationNotDoubleCounted = conservationNotDoubleCounted,
            MissingErosionNotInterpretedAsSafety = missingErosionNotSafety,
            IdempotentEnrichment = AreEnrichmentsEquivalent(firstEnrichment, secondEnrichment),
            IdempotentPersistence = idempotentPersistence,
            IdempotentKnowledgeGraphSync = idempotentGraphSync,
            NoFabricatedEvidenceWhenUnavailable = noFabricatedEvidence
        };
    }

    private static bool AreEnrichmentsEquivalent(
        LandParcelGisEnrichmentResult first,
        LandParcelGisEnrichmentResult second)
    {
        return first.OverallStatus == second.OverallStatus
            && first.Administrative?.Status == second.Administrative?.Status
            && first.RoadAccessibility?.RoadId == second.RoadAccessibility?.RoadId
            && first.RoadAccessibility?.DistanceMeters == second.RoadAccessibility?.DistanceMeters
            && first.WaterProximity?.FeatureId == second.WaterProximity?.FeatureId
            && first.WaterProximity?.DistanceMeters == second.WaterProximity?.DistanceMeters
            && first.Soil?.PrimarySoilGroupId == second.Soil?.PrimarySoilGroupId
            && first.Environmental?.IntersectsSoilConservationArea == second.Environmental?.IntersectsSoilConservationArea
            && first.Environmental?.ErosionDataStatus == second.Environmental?.ErosionDataStatus;
    }

    private async Task<bool> VerifyPersistenceIdempotencyAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var roadCount = await _dbContext.InfrastructureFeatures
            .CountAsync(
                feature => feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Road,
                cancellationToken);

        var waterCount = await _dbContext.InfrastructureFeatures
            .CountAsync(
                feature => feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Other,
                cancellationToken);

        var soilCount = await _dbContext.ParcelDerivedSoilGroups
            .CountAsync(entity => entity.LandParcelId == parcelId, cancellationToken);

        var snapshotCount = await _dbContext.LandParcelGisEnrichmentSnapshots
            .CountAsync(snapshot => snapshot.LandParcelId == parcelId, cancellationToken);

        return roadCount <= 1 && waterCount <= 1 && soilCount <= 1 && snapshotCount <= 1;
    }

    private async Task<HambantotaPilotValidationKnowledgeGraphResult> BuildKnowledgeGraphResultAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var intelligence = await _knowledgeGraphService.GetParcelGisGraphIntelligenceAsync(parcelId, cancellationToken);
        if (intelligence is null)
        {
            return new HambantotaPilotValidationKnowledgeGraphResult
            {
                Status = "Unavailable",
                Notes = "No GIS graph intelligence was returned for the parcel."
            };
        }

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcelId, cancellationToken);

        return new HambantotaPilotValidationKnowledgeGraphResult
        {
            Status = "Synced",
            HasRoadRelationship = intelligence.NearestRoad is not null,
            HasWaterRelationship = intelligence.NearestWater is not null,
            HasDerivedSoilRelationship = intelligence.DerivedSoil is not null,
            HasConservationRelationship = relationships.Any(relationship =>
                relationship.RelationshipType.Contains("Conservation", StringComparison.OrdinalIgnoreCase)),
            NearestWaterFeatureType = intelligence.NearestWater?.FeatureType,
            Notes = intelligence.NearestWater?.FeatureType == "WaterSupply"
                ? "Unexpected WaterSupply feature type in graph."
                : null
        };
    }

    private async Task<bool> VerifyGraphSyncIdempotencyAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var first = await _knowledgeGraphService.GetRelationshipsAsync(parcelId, cancellationToken);
        await _graphSyncService.SyncAsync(parcelId, cancellationToken);
        var second = await _knowledgeGraphService.GetRelationshipsAsync(parcelId, cancellationToken);

        if (first.Count != second.Count)
        {
            return false;
        }

        var firstKeys = first
            .Select(relationship => $"{relationship.RelationshipType}:{relationship.TargetNodeId}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        var secondKeys = second
            .Select(relationship => $"{relationship.RelationshipType}:{relationship.TargetNodeId}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        return firstKeys.SequenceEqual(secondKeys, StringComparer.Ordinal);
    }

    private static HambantotaPilotValidationReport BuildReport(
        IReadOnlyList<HambantotaPilotValidationScenarioResult> scenarios)
    {
        var behaviourChecks = scenarios
            .SelectMany(scenario => EnumerateBehaviourChecks(scenario.BehaviourChecks))
            .ToList();

        return new HambantotaPilotValidationReport
        {
            PilotDistrict = GisReferenceDataPaths.HambantotaDistrictName,
            GeneratedAt = DateTimeOffset.UtcNow,
            Neo4jAvailable = Neo4jSettings.IsConfigured(),
            Scenarios = scenarios,
            Summary = new HambantotaPilotValidationSummary
            {
                ScenarioCount = scenarios.Count,
                ScenariosWithAvailableGis = scenarios.Count(scenario =>
                    scenario.Metrics.GisEnrichmentOverallStatus == LandParcelGisEnrichmentOverallStatus.Complete.ToString()),
                ScenariosWithPartialGis = scenarios.Count(scenario =>
                    scenario.Metrics.GisEnrichmentOverallStatus == LandParcelGisEnrichmentOverallStatus.Partial.ToString()),
                ScenariosWithUnavailableGis = scenarios.Count(scenario =>
                    scenario.Metrics.GisEnrichmentOverallStatus == LandParcelGisEnrichmentOverallStatus.Unavailable.ToString()),
                RecommendationRuns = scenarios.Sum(scenario => scenario.Recommendations.Count),
                RecommendationsProduced = scenarios.Sum(scenario => scenario.Recommendations.Count(recommendation => recommendation.IsRecommended)),
                BehaviourChecksPassed = behaviourChecks.Count(check => check),
                BehaviourChecksTotal = behaviourChecks.Count
            }
        };
    }

    private static IEnumerable<bool> EnumerateBehaviourChecks(HambantotaPilotValidationBehaviourChecks checks)
    {
        yield return checks.OfficialSoilTypeUnchanged;
        yield return checks.OfficialProvinceUnchanged;
        yield return checks.OfficialDistrictUnchanged;
        yield return checks.NaturalWaterNotTreatedAsWaterSupply;
        yield return checks.GisSoilSeparateFromOfficialSoil;
        yield return checks.ConservationNotDoubleCounted;
        yield return checks.MissingErosionNotInterpretedAsSafety;
        yield return checks.IdempotentEnrichment;
        yield return checks.IdempotentPersistence;
        yield return checks.IdempotentKnowledgeGraphSync;
        yield return checks.NoFabricatedEvidenceWhenUnavailable;
    }
}
