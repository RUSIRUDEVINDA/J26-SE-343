using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class LandParcelGisKnowledgeGraphIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private ILandParcelGisEnrichmentService? _enrichmentService;
    private ILandParcelGisEnrichmentPersistenceService? _persistenceService;
    private ILandParcelGisKnowledgeGraphSyncService? _graphSyncService;
    private IKnowledgeGraphService? _knowledgeGraphService;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private readonly List<Guid> _createdParcelIds = [];

    public async Task InitializeAsync()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(LandIntelligenceIntegrationConfiguration.LoadApiConfiguration());

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _enrichmentService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
        _persistenceService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
        _graphSyncService = _serviceProvider.GetRequiredService<ILandParcelGisKnowledgeGraphSyncService>();
        _knowledgeGraphService = _serviceProvider.GetRequiredService<IKnowledgeGraphService>();

        var dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();
        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync(dbContext);
    }

    [Fact]
    public async Task SyncAsync_creates_gis_graph_relationships_for_partial_hambantota_enrichment()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_repository);
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, enrichment.OverallStatus);

        await _persistenceService.PersistAsync(enrichment);
        await _graphSyncService.SyncAsync(parcel.Id);

        var intelligence = await _knowledgeGraphService.GetParcelGisGraphIntelligenceAsync(parcel.Id);
        Assert.NotNull(intelligence);
        Assert.Equal(parcel.Id, intelligence!.ParcelId);
        Assert.NotNull(intelligence.ProvinceReferenceId);
        Assert.NotNull(intelligence.DistrictReferenceId);
        Assert.NotNull(intelligence.NearestRoad);
        Assert.NotNull(intelligence.NearestWater);
        Assert.NotNull(intelligence.DerivedSoil);
        Assert.NotEqual("WaterSupply", intelligence.NearestWater!.FeatureType);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.LocatedIn);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.NearRoad);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.NearWater);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.HasDerivedSoil);
        Assert.DoesNotContain(relationships, relationship => relationship.TargetNodeType == "WaterSupply");
        Assert.DoesNotContain(relationships, relationship => relationship.RelationshipType.Contains("Erosion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SyncAsync_is_idempotent_for_repeated_runs()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        await _persistenceService.PersistAsync(enrichment);

        await _graphSyncService.SyncAsync(parcel.Id);
        await _graphSyncService.SyncAsync(parcel.Id);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.Equal(1, relationships.Count(relationship => relationship.RelationshipType == GraphRelationshipTypes.NearRoad));
        Assert.Equal(1, relationships.Count(relationship => relationship.RelationshipType == GraphRelationshipTypes.NearWater));
        Assert.Equal(1, relationships.Count(relationship => relationship.RelationshipType == GraphRelationshipTypes.HasDerivedSoil));
    }

    [Fact]
    public async Task SyncAsync_does_not_create_gis_relationships_when_enrichment_is_unavailable()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var parcel = await PersistParcelAsync(6.9271, 79.8612);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, enrichment.OverallStatus);

        await _persistenceService.PersistAsync(enrichment);
        await _graphSyncService.SyncAsync(parcel.Id);

        var intelligence = await _knowledgeGraphService.GetParcelGisGraphIntelligenceAsync(parcel.Id);
        Assert.NotNull(intelligence);
        Assert.Null(intelligence!.NearestRoad);
        Assert.Null(intelligence.NearestWater);
        Assert.Null(intelligence.DerivedSoil);
    }

    [Fact]
    public async Task GetParcelIdsByDerivedSoilGroupAsync_finds_parcels_sharing_soil_group()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var first = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var second = await PersistParcelAsync(_hambantotaLatitude + 0.0001, _hambantotaLongitude + 0.0001);

        var firstEnrichment = await _enrichmentService.EnrichAsync(first.Id);
        var secondEnrichment = await _enrichmentService.EnrichAsync(second.Id);
        await _persistenceService.PersistAsync(firstEnrichment);
        await _persistenceService.PersistAsync(secondEnrichment);
        await _graphSyncService.SyncAsync(first.Id);
        await _graphSyncService.SyncAsync(second.Id);

        Assert.NotNull(firstEnrichment.Soil?.PrimarySoilGroupId);
        var parcelIds = await _knowledgeGraphService.GetParcelIdsByDerivedSoilGroupAsync(
            firstEnrichment.Soil.PrimarySoilGroupId.Value);

        Assert.Contains(first.Id, parcelIds);
        Assert.Contains(second.Id, parcelIds);
    }

    [Fact]
    public async Task SyncAsync_preserves_unrelated_manual_graph_relationships()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var categoryId = KnowledgeGraphSeedData.GetCategoryId(LandCategoryType.StateLand);

        await _knowledgeGraphService.UpsertLandCategoryAsync(
            KnowledgeGraphSeedData.Categories.First(category => category.Id == categoryId));
        await _knowledgeGraphService.UpsertLandParcelAsync(
            new LandParcelGraphNodeDto(parcel.Id, parcel.Identifier.CadastralNumber, parcel.Identifier.SurveyPlanReference));
        await _knowledgeGraphService.LinkParcelToCategoryAsync(parcel.Id, categoryId);

        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        await _persistenceService.PersistAsync(enrichment);
        await _graphSyncService.SyncAsync(parcel.Id);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.HasCategory);
        Assert.Contains(relationships, relationship => relationship.RelationshipType == GraphRelationshipTypes.NearRoad);
    }

    [Fact]
    public async Task SyncAsync_replaces_stale_gis_road_relationship_when_h10_evidence_changes()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var dbContext = _serviceProvider!.GetRequiredService<LandIntelligenceDbContext>();
        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        await _persistenceService.PersistAsync(enrichment);
        await _graphSyncService.SyncAsync(parcel.Id);

        var roadFeatures = await dbContext.InfrastructureFeatures
            .Where(feature =>
                feature.LandParcelId == parcel.Id
                && feature.Type == InfrastructureFeatureType.Road)
            .ToListAsync();
        var roadFeature = roadFeatures
            .First(feature => LandParcelGisEnrichmentPersistenceMapper.IsGisDerivedInfrastructure(feature));
        roadFeature.DistanceMeters = (roadFeature.DistanceMeters ?? 0m) + 500m;
        await dbContext.SaveChangesAsync();

        await _graphSyncService.SyncAsync(parcel.Id);

        var intelligence = await _knowledgeGraphService.GetParcelGisGraphIntelligenceAsync(parcel.Id);
        Assert.NotNull(intelligence?.NearestRoad);
        AssertStoredDistanceMeters(roadFeature.DistanceMeters!.Value, intelligence!.NearestRoad!.DistanceMeters);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.Equal(1, relationships.Count(relationship => relationship.RelationshipType == GraphRelationshipTypes.NearRoad));
    }

    [Fact]
    public async Task SyncAsync_retains_gis_provenance_on_graph_relationships()
    {
        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_persistenceService);
        Assert.NotNull(_graphSyncService);
        Assert.NotNull(_knowledgeGraphService);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);
        var enrichment = await _enrichmentService.EnrichAsync(parcel.Id);
        await _persistenceService.PersistAsync(enrichment);
        await _graphSyncService.SyncAsync(parcel.Id);

        var intelligence = await _knowledgeGraphService.GetParcelGisGraphIntelligenceAsync(parcel.Id);
        Assert.NotNull(intelligence);
        Assert.Equal(GisGraphRelationshipOwnership.SourceName, intelligence!.NearestRoad?.SourceName);
        Assert.Equal(GisGraphRelationshipOwnership.SourceName, intelligence.NearestWater?.SourceName);
        Assert.Equal(GisGraphRelationshipOwnership.SourceName, intelligence.DerivedSoil?.SourceName);
        Assert.True(intelligence.NearestRoad?.DerivedAt <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// H10 persists road/water distances as PostgreSQL numeric(12,2); Neo4j round-trips the same stored value.
    /// </summary>
    private static void AssertStoredDistanceMeters(decimal expected, decimal actual) =>
        Assert.Equal(expected, actual, precision: 2);

    private async Task<LandParcel> PersistParcelAsync(double latitude, double longitude)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H11-GRAPH-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H11-GRAPH-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] H11 graph sync test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude));

        await _repository.AddAsync(parcel);
        _createdParcelIds.Add(parcel.Id);
        return parcel;
    }

    private static async Task<(double Latitude, double Longitude)> ReadHambantotaInteriorPointAsync(
        LandIntelligenceDbContext dbContext)
    {
        const string sql = """
            SELECT
                ST_Y(ST_PointOnSurface("Boundary")) AS "Latitude",
                ST_X(ST_PointOnSurface("Boundary")) AS "Longitude"
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @districtName
              AND "BoundaryType" = @districtType
            LIMIT 1
            """;

        var connection = dbContext.Database.GetDbConnection();
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
        if (_serviceProvider is null)
        {
            return;
        }

        var dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        if (_knowledgeGraphService is not null)
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
            var parcels = await dbContext.LandParcels
                .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                .ToListAsync();

            dbContext.LandParcels.RemoveRange(parcels);
            await dbContext.SaveChangesAsync();
        }

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(dbContext);
        await _serviceProvider.DisposeAsync();
    }
}
