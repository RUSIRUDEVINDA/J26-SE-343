using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PilotValidation;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
[Trait("Milestone", "H13")]
public sealed class HambantotaPilotValidationIntegrationTests : IAsyncLifetime
{
    private const string OfficialSoilType = "Official Commissioner Loam";

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private IHambantotaPilotValidationService? _validationService;
    private ILandParcelRepository? _repository;
    private IKnowledgeGraphService? _knowledgeGraphService;
    private LandIntelligenceDbContext? _dbContext;

    private readonly List<Guid> _createdParcelIds = [];

    public HambantotaPilotValidationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(LandIntelligenceIntegrationConfiguration.LoadApiConfiguration());

        _serviceProvider = services.BuildServiceProvider();
        _validationService = _serviceProvider.GetRequiredService<IHambantotaPilotValidationService>();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _knowledgeGraphService = _serviceProvider.GetRequiredService<IKnowledgeGraphService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));
    }

    [Fact]
    public async Task ValidateScenariosAsync_produces_complete_hambantota_pilot_report()
    {
        Assert.NotNull(_validationService);
        Assert.NotNull(_dbContext);

        var interior = await HambantotaPilotGisSampleQueries.ReadInteriorPointAsync(_dbContext);
        var strongRoad = await HambantotaPilotGisSampleQueries.ReadRoadStartPointAsync(_dbContext);
        var weakRoad = await HambantotaPilotGisSampleQueries.ReadWeakestRoadAccessibilityPointAsync(_dbContext);
        var conservation = await HambantotaPilotGisSampleQueries.ReadConservationSampleAsync(_dbContext);
        var soil = await HambantotaPilotGisSampleQueries.ReadSoilGroupSampleAsync(_dbContext);
        var water = await HambantotaPilotGisSampleQueries.ReadNaturalWaterSampleAsync(_dbContext);

        var landUsePurposes = new[]
        {
            LandUseType.Agricultural,
            LandUseType.Residential,
            LandUseType.Commercial
        };

        var requests = new List<HambantotaPilotValidationRequest>
        {
            await CreateScenarioRequestAsync(
                "StrongRoadAccessibility",
                "Parcel on mapped road geometry with near-zero GIS road distance.",
                strongRoad.Latitude,
                strongRoad.Longitude,
                landUsePurposes),
            await CreateScenarioRequestAsync(
                "WeakerRoadAccessibility",
                "Parcel at a Hambantota interior sample point with the longest mapped road distance among soil-group samples.",
                weakRoad.Latitude,
                weakRoad.Longitude,
                landUsePurposes),
            await CreateScenarioRequestAsync(
                "ConservationIntersection",
                "Parcel intersecting a mapped soil conservation area within Hambantota.",
                conservation.Latitude,
                conservation.Longitude,
                landUsePurposes,
                boundary: CreateSmallBoundaryAround(conservation.Latitude, conservation.Longitude, delta: 0.02)),
            await CreateScenarioRequestAsync(
                "GisDerivedSoil",
                "Parcel within a mapped GIS soil group polygon while preserving official soil type.",
                soil.Latitude,
                soil.Longitude,
                landUsePurposes,
                boundary: CreateSmallBoundaryAround(soil.Latitude, soil.Longitude, delta: 0.001),
                officialSoilType: OfficialSoilType),
            await CreateScenarioRequestAsync(
                "NaturalWaterProximity",
                "Parcel on mapped natural water (canal) geometry.",
                water.Latitude,
                water.Longitude,
                landUsePurposes),
            await CreateScenarioRequestAsync(
                "PartialEnrichmentMissingErosion",
                "Parcel inside Hambantota pilot with partial GIS enrichment because erosion observations are unavailable.",
                interior.Latitude,
                interior.Longitude,
                landUsePurposes),
            await CreateScenarioRequestAsync(
                "OutsideGisCoverage",
                "Parcel outside imported Hambantota GIS coverage.",
                HambantotaPilotGisSampleQueries.OutsideCoverageLatitude,
                HambantotaPilotGisSampleQueries.OutsideCoverageLongitude,
                landUsePurposes)
        };

        var report = await _validationService.ValidateScenariosAsync(requests);

        _output.WriteLine(HambantotaPilotValidationReportWriter.ToHumanReadableSummary(report));
        _output.WriteLine(HambantotaPilotValidationReportWriter.ToJson(report));

        Assert.Equal(7, report.Scenarios.Count);
        Assert.Equal(21, report.Summary.RecommendationRuns);
        Assert.True(report.Summary.ScenariosWithPartialGis >= 5);
        Assert.Equal(1, report.Summary.ScenariosWithUnavailableGis);

        foreach (var scenario in report.Scenarios.Where(item => item.Metrics.ScenarioKey != "OutsideGisCoverage"))
        {
            AssertAllBehaviourChecks(scenario);
            Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial.ToString(), scenario.Metrics.GisEnrichmentOverallStatus);
            Assert.Equal(ErosionDataStatus.Unavailable.ToString(), scenario.Metrics.ErosionDataAvailability);
        }

        var outside = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "OutsideGisCoverage");
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable.ToString(), outside.Metrics.GisEnrichmentOverallStatus);
        Assert.True(outside.BehaviourChecks.NoFabricatedEvidenceWhenUnavailable);
        Assert.Null(outside.Metrics.MappedRoadDistanceMeters);
        Assert.Null(outside.Metrics.GisDerivedSoilGroup);

        var soilScenario = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "GisDerivedSoil");
        Assert.Equal(OfficialSoilType, soilScenario.Metrics.OfficialSoilType);
        Assert.NotNull(soilScenario.Metrics.GisDerivedSoilGroup);
        Assert.NotEqual(soilScenario.Metrics.OfficialSoilType, soilScenario.Metrics.GisDerivedSoilGroup);
        Assert.True(soilScenario.BehaviourChecks.GisSoilSeparateFromOfficialSoil);

        var waterScenario = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "NaturalWaterProximity");
        Assert.True(waterScenario.BehaviourChecks.NaturalWaterNotTreatedAsWaterSupply);
        Assert.DoesNotContain(
            waterScenario.Recommendations.SelectMany(recommendation => recommendation.Evidence),
            evidence => evidence.Description.Contains("WaterSupply", StringComparison.OrdinalIgnoreCase));

        var conservationScenario = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "ConservationIntersection");
        Assert.True(conservationScenario.Metrics.ConservationIntersection);
        Assert.True(conservationScenario.BehaviourChecks.ConservationNotDoubleCounted);

        var strongRoadScenario = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "StrongRoadAccessibility");
        Assert.NotNull(strongRoadScenario.Metrics.MappedRoadDistanceMeters);
        Assert.True(strongRoadScenario.Metrics.MappedRoadDistanceMeters <= 25m);

        var weakRoadScenario = report.Scenarios.Single(scenario => scenario.Metrics.ScenarioKey == "WeakerRoadAccessibility");
        Assert.NotNull(weakRoadScenario.Metrics.MappedRoadDistanceMeters);
        Assert.True(weakRoadScenario.Metrics.MappedRoadDistanceMeters > strongRoadScenario.Metrics.MappedRoadDistanceMeters);

        foreach (var landUse in landUsePurposes)
        {
            var partialRecommendation = report.Scenarios
                .Single(scenario => scenario.Metrics.ScenarioKey == "PartialEnrichmentMissingErosion")
                .Recommendations
                .Single(recommendation => recommendation.RequestedPurpose == landUse);

            Assert.True(partialRecommendation.IsRecommended);
            Assert.NotNull(partialRecommendation.SuitabilityScore);
            Assert.Contains(
                partialRecommendation.Evidence,
                evidence => evidence.Description.Contains("GIS-derived soil group", StringComparison.OrdinalIgnoreCase)
                    || evidence.Description.Contains("mapped", StringComparison.OrdinalIgnoreCase));
        }

        if (Neo4jSettings.IsConfigured())
        {
            var syncedScenario = report.Scenarios.First(scenario => scenario.KnowledgeGraph?.Status == "Synced");
            Assert.NotNull(syncedScenario.KnowledgeGraph?.HasRoadRelationship);
            Assert.NotEqual("WaterSupply", syncedScenario.KnowledgeGraph?.NearestWaterFeatureType);
        }
    }

    [Fact]
    public async Task ValidateScenarioAsync_max_road_distance_filter_excludes_distant_parcels()
    {
        Assert.NotNull(_validationService);
        Assert.NotNull(_dbContext);

        var strongRoad = await HambantotaPilotGisSampleQueries.ReadRoadStartPointAsync(_dbContext);
        var weakRoad = await HambantotaPilotGisSampleQueries.ReadWeakestRoadAccessibilityPointAsync(_dbContext);

        var strongParcel = await PersistParcelAsync("H13-FILTER-STRONG", strongRoad.Latitude, strongRoad.Longitude);
        var weakParcel = await PersistParcelAsync("H13-FILTER-WEAK", weakRoad.Latitude, weakRoad.Longitude);

        var strongBaseline = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = strongParcel.Id,
            ScenarioKey = "RoadFilterStrongBaseline",
            ScenarioDescription = "Measure GIS road distance for on-road parcel.",
            LandUsePurposes = [],
            MaxRoadDistanceMeters = 50_000m,
            SyncToKnowledgeGraph = false
        });

        var weakBaseline = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = weakParcel.Id,
            ScenarioKey = "RoadFilterWeakBaseline",
            ScenarioDescription = "Measure GIS road distance for distant parcel.",
            LandUsePurposes = [],
            MaxRoadDistanceMeters = 50_000m,
            SyncToKnowledgeGraph = false
        });

        Assert.NotNull(strongBaseline.Metrics.MappedRoadDistanceMeters);
        Assert.NotNull(weakBaseline.Metrics.MappedRoadDistanceMeters);
        Assert.True(weakBaseline.Metrics.MappedRoadDistanceMeters > strongBaseline.Metrics.MappedRoadDistanceMeters);

        var tightFilterMeters = Math.Max(
            strongBaseline.Metrics.MappedRoadDistanceMeters.Value + 50m,
            weakBaseline.Metrics.MappedRoadDistanceMeters.Value / 2m);

        Assert.True(tightFilterMeters < weakBaseline.Metrics.MappedRoadDistanceMeters);

        var strongResult = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = strongParcel.Id,
            ScenarioKey = "RoadFilterStrong",
            ScenarioDescription = "Tight road distance filter should include on-road parcel.",
            LandUsePurposes = [LandUseType.Agricultural],
            MaxRoadDistanceMeters = tightFilterMeters,
            RequireRoadAccess = true,
            SyncToKnowledgeGraph = false
        });

        var weakResult = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = weakParcel.Id,
            ScenarioKey = "RoadFilterWeak",
            ScenarioDescription = "Tight road distance filter should fail accessibility for distant parcel.",
            LandUsePurposes = [LandUseType.Agricultural],
            MaxRoadDistanceMeters = tightFilterMeters,
            RequireRoadAccess = true,
            SyncToKnowledgeGraph = false
        });

        var strongAccessibility = Assert.Single(strongResult.Recommendations).MatchingCriteria
            .Concat(Assert.Single(strongResult.Recommendations).FailedCriteria)
            .Single(criterion => criterion.Key == "accessibility");
        var weakAccessibility = Assert.Single(weakResult.Recommendations).MatchingCriteria
            .Concat(Assert.Single(weakResult.Recommendations).FailedCriteria)
            .Single(criterion => criterion.Key == "accessibility");

        Assert.True(strongAccessibility.IsMet);
        Assert.False(weakAccessibility.IsMet);
        Assert.Contains("Nearest mapped road", weakAccessibility.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateScenarioAsync_closer_mapped_road_improves_accessibility_ranking()
    {
        Assert.NotNull(_validationService);
        Assert.NotNull(_dbContext);

        var strongRoad = await HambantotaPilotGisSampleQueries.ReadRoadStartPointAsync(_dbContext);
        var weakRoad = await HambantotaPilotGisSampleQueries.ReadWeakestRoadAccessibilityPointAsync(_dbContext);

        var strongParcel = await PersistParcelAsync("H13-RANK-STRONG", strongRoad.Latitude, strongRoad.Longitude);
        var weakParcel = await PersistParcelAsync("H13-RANK-WEAK", weakRoad.Latitude, weakRoad.Longitude);

        var strongResult = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = strongParcel.Id,
            ScenarioKey = "RoadRankingStrong",
            ScenarioDescription = "Closer mapped road parcel for accessibility comparison.",
            LandUsePurposes = [LandUseType.Agricultural],
            MaxRoadDistanceMeters = 50_000m,
            SyncToKnowledgeGraph = false
        });

        var weakResult = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = weakParcel.Id,
            ScenarioKey = "RoadRankingWeak",
            ScenarioDescription = "Distant mapped road parcel for accessibility comparison.",
            LandUsePurposes = [LandUseType.Agricultural],
            MaxRoadDistanceMeters = 50_000m,
            SyncToKnowledgeGraph = false
        });

        var strongRecommendation = Assert.Single(strongResult.Recommendations);
        var weakRecommendation = Assert.Single(weakResult.Recommendations);

        Assert.True(strongRecommendation.SuitabilityScore >= weakRecommendation.SuitabilityScore);

        var strongAccessibilityScore = strongRecommendation.MatchingCriteria
            .Concat(strongRecommendation.FailedCriteria)
            .Single(criterion => criterion.Key == "accessibility").Score;
        var weakAccessibilityScore = weakRecommendation.MatchingCriteria
            .Concat(weakRecommendation.FailedCriteria)
            .Single(criterion => criterion.Key == "accessibility").Score;

        Assert.True(strongAccessibilityScore >= weakAccessibilityScore);
        Assert.NotNull(strongResult.Metrics.MappedRoadDistanceMeters);
        Assert.NotNull(weakResult.Metrics.MappedRoadDistanceMeters);
        Assert.True(weakResult.Metrics.MappedRoadDistanceMeters > strongResult.Metrics.MappedRoadDistanceMeters);
    }

    [Fact]
    public async Task ValidateScenarioAsync_preserves_official_parcel_fields_and_remains_deterministic()
    {
        Assert.NotNull(_validationService);
        Assert.NotNull(_dbContext);

        var interior = await HambantotaPilotGisSampleQueries.ReadInteriorPointAsync(_dbContext);
        var parcel = await PersistParcelAsync(
            "H13-IDEMPOTENT",
            interior.Latitude,
            interior.Longitude,
            officialSoilType: OfficialSoilType);

        var first = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = parcel.Id,
            ScenarioKey = "IdempotencyFirstRun",
            ScenarioDescription = "First H13 validation run for determinism checks.",
            LandUsePurposes = [LandUseType.Agricultural],
            SyncToKnowledgeGraph = Neo4jSettings.IsConfigured()
        });

        var second = await _validationService.ValidateScenarioAsync(new HambantotaPilotValidationRequest
        {
            ParcelId = parcel.Id,
            ScenarioKey = "IdempotencySecondRun",
            ScenarioDescription = "Second H13 validation run for determinism checks.",
            LandUsePurposes = [LandUseType.Agricultural],
            SyncToKnowledgeGraph = Neo4jSettings.IsConfigured()
        });

        Assert.True(first.BehaviourChecks.OfficialSoilTypeUnchanged);
        Assert.True(first.BehaviourChecks.OfficialProvinceUnchanged);
        Assert.True(first.BehaviourChecks.OfficialDistrictUnchanged);
        Assert.True(first.BehaviourChecks.IdempotentEnrichment);
        Assert.True(first.BehaviourChecks.IdempotentPersistence);
        Assert.True(first.BehaviourChecks.MissingErosionNotInterpretedAsSafety);

        Assert.Equal(first.Metrics.GisEnrichmentOverallStatus, second.Metrics.GisEnrichmentOverallStatus);
        Assert.Equal(first.Metrics.MappedRoadDistanceMeters, second.Metrics.MappedRoadDistanceMeters);
        Assert.Equal(first.Metrics.MappedWaterDistanceMeters, second.Metrics.MappedWaterDistanceMeters);
        Assert.Equal(first.Metrics.GisDerivedSoilGroup, second.Metrics.GisDerivedSoilGroup);
        Assert.Equal(
            first.Recommendations[0].SuitabilityScore,
            second.Recommendations[0].SuitabilityScore);
    }

    private async Task<HambantotaPilotValidationRequest> CreateScenarioRequestAsync(
        string scenarioKey,
        string description,
        double latitude,
        double longitude,
        IReadOnlyList<LandUseType> landUsePurposes,
        GeoBoundary? boundary = null,
        string? officialSoilType = null)
    {
        var parcel = await PersistParcelAsync($"H13-{scenarioKey}", latitude, longitude, boundary, officialSoilType);

        return new HambantotaPilotValidationRequest
        {
            ParcelId = parcel.Id,
            ScenarioKey = scenarioKey,
            ScenarioDescription = description,
            UsesSyntheticGisData = false,
            LandUsePurposes = landUsePurposes,
            MaxRoadDistanceMeters = 20_000m,
            RequireRoadAccess = true,
            SyncToKnowledgeGraph = Neo4jSettings.IsConfigured()
        };
    }

    private async Task<LandParcel> PersistParcelAsync(
        string cadastralPrefix,
        double latitude,
        double longitude,
        GeoBoundary? boundary = null,
        string? officialSoilType = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"{cadastralPrefix}-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H13-PILOT-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] H13 Hambantota pilot validation parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude, boundary: boundary),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC] H13 validation parcel use"),
            officialSoilType is null
                ? null
                : new LandCharacteristics(
                    officialSoilType,
                    terrainDescription: null,
                    elevationMeters: null,
                    soilTypeProvenance: AttributeProvenance.Official("Land Commissioner")));

        await _repository.AddAsync(parcel);
        _createdParcelIds.Add(parcel.Id);
        return parcel;
    }

    private static GeoBoundary CreateSmallBoundaryAround(double latitude, double longitude, double delta) =>
        new([
            new GeoCoordinate(latitude - delta, longitude - delta),
            new GeoCoordinate(latitude - delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude - delta)
        ]);

    private static void AssertAllBehaviourChecks(HambantotaPilotValidationScenarioResult scenario)
    {
        var checks = scenario.BehaviourChecks;
        Assert.True(checks.OfficialSoilTypeUnchanged, $"{scenario.Metrics.ScenarioKey}: official soil changed");
        Assert.True(checks.OfficialProvinceUnchanged, $"{scenario.Metrics.ScenarioKey}: official province changed");
        Assert.True(checks.OfficialDistrictUnchanged, $"{scenario.Metrics.ScenarioKey}: official district changed");
        Assert.True(checks.NaturalWaterNotTreatedAsWaterSupply, $"{scenario.Metrics.ScenarioKey}: water supply confusion");
        Assert.True(checks.GisSoilSeparateFromOfficialSoil, $"{scenario.Metrics.ScenarioKey}: GIS soil not separate");
        Assert.True(checks.ConservationNotDoubleCounted, $"{scenario.Metrics.ScenarioKey}: conservation double-counted");
        Assert.True(checks.MissingErosionNotInterpretedAsSafety, $"{scenario.Metrics.ScenarioKey}: erosion safety implied");
        Assert.True(checks.IdempotentEnrichment, $"{scenario.Metrics.ScenarioKey}: enrichment not idempotent");
        Assert.True(checks.IdempotentPersistence, $"{scenario.Metrics.ScenarioKey}: persistence not idempotent");
        Assert.True(checks.IdempotentKnowledgeGraphSync, $"{scenario.Metrics.ScenarioKey}: graph sync not idempotent");
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is null || _dbContext is null)
        {
            return;
        }

        if (_knowledgeGraphService is not null && Neo4jSettings.IsConfigured())
        {
            foreach (var parcelId in _createdParcelIds)
            {
                try
                {
                    await _knowledgeGraphService.DeleteLandParcelGraphAsync(parcelId);
                }
                catch (Exception)
                {
                    // Best-effort cleanup when Neo4j is configured but unavailable.
                }
            }
        }

        if (_createdParcelIds.Count > 0)
        {
            var parcels = await _dbContext.LandParcels
                .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                .ToListAsync();

            _dbContext.LandParcels.RemoveRange(parcels);
            await _dbContext.SaveChangesAsync();
        }

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);
        await _serviceProvider.DisposeAsync();
    }
}
