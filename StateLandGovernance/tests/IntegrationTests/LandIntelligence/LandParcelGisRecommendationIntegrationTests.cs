using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class LandParcelGisRecommendationIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private ILandParcelGisEnrichmentService? _enrichmentService;
    private ILandParcelGisEnrichmentPersistenceService? _persistenceService;
    private ILandRecommendationEngine? _recommendationEngine;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private readonly List<Guid> _createdParcelIds = [];

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(LandIntelligenceIntegrationConfiguration.LoadApiConfiguration());

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _enrichmentService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
        _persistenceService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
        _recommendationEngine = _serviceProvider.GetRequiredService<ILandRecommendationEngine>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
    }

    [Fact]
    public async Task RecommendAsync_uses_persisted_hambantota_gis_intelligence_end_to_end()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_recommendationEngine);
        Assert.NotNull(_dbContext);

        const string officialSoilType = "Official Commissioner Loam";
        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude, officialSoilType);

        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, enrichment.OverallStatus);

        await _persistenceService.PersistAsync(enrichment);

        var reloaded = await _repository.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(officialSoilType, reloaded!.Characteristics?.SoilType);
        Assert.NotNull(reloaded.GisDerivedIntelligence);
        Assert.Equal(GisEnrichmentOverallStatus.Partial, reloaded.GisDerivedIntelligence!.EnrichmentStatus);
        Assert.NotNull(reloaded.GisDerivedIntelligence.DerivedSoilGroup);

        var roadFeature = reloaded.InfrastructureFeatures
            .FirstOrDefault(feature =>
                feature.Type == InfrastructureFeatureType.Road
                && feature.DistanceProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName);
        Assert.NotNull(roadFeature);

        var response = await _recommendationEngine.RecommendAsync(new LandRecommendationSearchRequest
        {
            TargetParcelId = parcel.Id,
            RequiredPurpose = LandUseType.Agricultural,
            RequiredLandCategory = LandCategoryType.StateLand,
            RequiredLandUse = LandUseType.Agricultural,
            RequiredAreaHectares = 0.5m,
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 20000m
            },
            Environmental = new EnvironmentalCriteria
            {
                MaxAllowedEnvironmentalSeverity = RestrictionSeverity.Medium,
                RejectProhibitiveEnvironmentalRestrictions = true
            },
            MaxResults = 1
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.Equal(parcel.Id, recommendation.ParcelId);

        var accessibility = recommendation.MatchingCriteria
            .Concat(recommendation.FailedCriteria)
            .Single(criterion => criterion.Key == "accessibility");
        Assert.Contains("Nearest mapped road", accessibility.Summary, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(recommendation.Evidence, evidence =>
            evidence.Description.Contains("GIS-derived soil group", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("erosion observations are unavailable", recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(recommendation.Evidence, evidence =>
            evidence.Description.Contains("WaterSupply", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        string? officialSoilType = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H12-REC-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H12-REC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] H12 recommendation GIS test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC] Agricultural use"),
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

    private async Task<(double Latitude, double Longitude)> ReadHambantotaInteriorPointAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                ST_Y(ST_PointOnSurface("Boundary")) AS "Latitude",
                ST_X(ST_PointOnSurface("Boundary")) AS "Longitude"
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @districtName
              AND "BoundaryType" = @districtType
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var districtNameParameter = command.CreateParameter();
        districtNameParameter.ParameterName = "districtName";
        districtNameParameter.Value = GisReferenceDataPaths.HambantotaDistrictName;
        command.Parameters.Add(districtNameParameter);

        var districtTypeParameter = command.CreateParameter();
        districtTypeParameter.ParameterName = "districtType";
        districtTypeParameter.Value = 2;
        command.Parameters.Add(districtTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected Hambantota interior point was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is null || _dbContext is null)
        {
            return;
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
