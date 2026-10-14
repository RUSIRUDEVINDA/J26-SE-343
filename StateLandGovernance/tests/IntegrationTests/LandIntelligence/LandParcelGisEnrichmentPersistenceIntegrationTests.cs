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
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class LandParcelGisEnrichmentPersistenceIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private ILandParcelGisEnrichmentService? _enrichmentService;
    private ILandParcelGisEnrichmentPersistenceService? _persistenceService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private readonly List<Guid> _createdParcelIds = [];

    public async Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _enrichmentService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
        _persistenceService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
    }

    [Fact]
    public async Task PersistAsync_preserves_official_parcel_values_for_real_hambantota_partial_result()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);

        const string officialSoilType = "Official Commissioner Loam";
        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude, officialSoilType);

        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, enrichment.OverallStatus);

        await _persistenceService.PersistAsync(enrichment);

        var reloaded = await _repository.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Southern Province", reloaded!.Location.Province);
        Assert.Equal("Hambantota", reloaded.Location.District);
        Assert.Equal(officialSoilType, reloaded.Characteristics?.SoilType);
    }

    [Fact]
    public async Task PersistAsync_is_idempotent_for_real_hambantota_enrichment()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);

        await _persistenceService.PersistAsync(enrichment);
        await _persistenceService.PersistAsync(enrichment);

        var roadCount = await _dbContext.InfrastructureFeatures
            .CountAsync(feature =>
                feature.LandParcelId == parcel.Id && feature.Type == InfrastructureFeatureType.Road);
        var waterCount = await _dbContext.InfrastructureFeatures
            .CountAsync(feature =>
                feature.LandParcelId == parcel.Id && feature.Type == InfrastructureFeatureType.Other);
        var soilCount = await _dbContext.ParcelDerivedSoilGroups.CountAsync(entity => entity.LandParcelId == parcel.Id);
        var snapshotCount = await _dbContext.LandParcelGisEnrichmentSnapshots
            .CountAsync(snapshot => snapshot.LandParcelId == parcel.Id);

        Assert.Equal(1, roadCount);
        Assert.Equal(1, waterCount);
        Assert.Equal(1, soilCount);
        Assert.Equal(1, snapshotCount);
    }

    [Fact]
    public async Task PersistAsync_does_not_fabricate_records_for_unavailable_enrichment()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(6.9271, 79.8612);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, enrichment.OverallStatus);

        await _persistenceService.PersistAsync(enrichment);

        Assert.Empty(await _dbContext.InfrastructureFeatures
            .Where(feature => feature.LandParcelId == parcel.Id)
            .ToListAsync());
        Assert.Empty(await _dbContext.ParcelDerivedSoilGroups
            .Where(entity => entity.LandParcelId == parcel.Id)
            .ToListAsync());
        Assert.Empty(await _dbContext.EnvironmentalRestrictions
            .Where(restriction => restriction.LandParcelId == parcel.Id)
            .ToListAsync());

        var snapshot = await _dbContext.LandParcelGisEnrichmentSnapshots
            .SingleAsync(entity => entity.LandParcelId == parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, snapshot.OverallStatus);
    }

    [Fact]
    public async Task PersistAsync_retains_derived_provenance_for_real_hambantota_result()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        await _persistenceService.PersistAsync(enrichment);

        var road = await _dbContext.InfrastructureFeatures
            .SingleAsync(feature => feature.LandParcelId == parcel.Id && feature.Type == InfrastructureFeatureType.Road);
        var soil = await _dbContext.ParcelDerivedSoilGroups.SingleAsync(entity => entity.LandParcelId == parcel.Id);
        var snapshot = await _dbContext.LandParcelGisEnrichmentSnapshots
            .SingleAsync(entity => entity.LandParcelId == parcel.Id);

        var roadProvenance = AttributeProvenancePersistenceMapper.Deserialize(road.DistanceProvenanceJson);
        var soilProvenance = AttributeProvenancePersistenceMapper.Deserialize(soil.ProvenanceJson);

        Assert.NotNull(roadProvenance);
        Assert.Equal(GisDerivedIntelligenceOwnership.SourceName, roadProvenance!.SourceName);
        Assert.NotNull(soilProvenance);
        Assert.Equal(GisDerivedIntelligenceOwnership.SourceName, soilProvenance!.SourceName);
        Assert.Equal(GisReferenceDataPaths.SourceName, snapshot.SourceName);
        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, snapshot.AdministrativeStatus);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        string? soilType = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H10-PERSIST-{Guid.NewGuid():N}"[..24];
        LandCharacteristics? characteristics = soilType is null
            ? null
            : new LandCharacteristics(soilType, terrainDescription: null, elevationMeters: null);

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H10-PERSIST-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] H10 persistence test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude),
            characteristics: characteristics);

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
        if (_dbContext is not null)
        {
            if (_createdParcelIds.Count > 0)
            {
                var parcels = await _dbContext.LandParcels
                    .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                    .ToListAsync();

                _dbContext.LandParcels.RemoveRange(parcels);
                await _dbContext.SaveChangesAsync();
            }

            await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}
