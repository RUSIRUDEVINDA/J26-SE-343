using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class LandParcelGisKnowledgeGraphSyncServiceTests
{
    [Fact]
    public async Task BuildSyncRequestAsync_reads_h10_persisted_state_without_rerunning_enrichment()
    {
        var parcelId = Guid.NewGuid();
        var roadReferenceId = Guid.NewGuid();
        var waterReferenceId = Guid.NewGuid();
        var soilReferenceId = Guid.NewGuid();

        await using var dbContext = CreateDbContext(parcelId, roadReferenceId, waterReferenceId, soilReferenceId);
        var service = new LandParcelGisKnowledgeGraphSyncService(
            dbContext,
            new NoOpKnowledgeGraphService(),
            NullLogger<LandParcelGisKnowledgeGraphSyncService>.Instance);

        var request = await service.BuildSyncRequestAsync(parcelId, CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, request!.OverallStatus);
        Assert.NotNull(request.Administrative);
        Assert.NotNull(request.Road);
        Assert.Equal(roadReferenceId, request.Road!.RoadReferenceId);
        Assert.NotNull(request.Water);
        Assert.Equal(waterReferenceId, request.Water!.WaterReferenceId);
        Assert.NotNull(request.Soil);
        Assert.Equal(soilReferenceId, request.Soil!.SoilGroupReferenceId);
        Assert.Empty(request.ConservationAreas);
    }

    [Fact]
    public async Task BuildSyncRequestAsync_marks_unavailable_when_snapshot_is_unavailable()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), unavailable: true);
        var service = new LandParcelGisKnowledgeGraphSyncService(
            dbContext,
            new NoOpKnowledgeGraphService(),
            NullLogger<LandParcelGisKnowledgeGraphSyncService>.Instance);

        var request = await service.BuildSyncRequestAsync(parcelId, CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, request!.OverallStatus);
        Assert.Null(request.Road);
        Assert.Null(request.Water);
        Assert.Null(request.Soil);
    }

    [Fact]
    public async Task BuildSyncRequestAsync_reflects_updated_h10_road_distance()
    {
        var parcelId = Guid.NewGuid();
        var roadReferenceId = Guid.NewGuid();
        var waterReferenceId = Guid.NewGuid();
        var soilReferenceId = Guid.NewGuid();

        await using var dbContext = CreateDbContext(parcelId, roadReferenceId, waterReferenceId, soilReferenceId);
        var service = new LandParcelGisKnowledgeGraphSyncService(
            dbContext,
            new NoOpKnowledgeGraphService(),
            NullLogger<LandParcelGisKnowledgeGraphSyncService>.Instance);

        var initialRequest = await service.BuildSyncRequestAsync(parcelId, CancellationToken.None);
        Assert.Equal(4100m, initialRequest!.Road!.DistanceMeters);

        var roadFeature = await dbContext.InfrastructureFeatures
            .FirstAsync(feature => feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Road);
        roadFeature.DistanceMeters = 4600m;
        await dbContext.SaveChangesAsync();

        var updatedRequest = await service.BuildSyncRequestAsync(parcelId, CancellationToken.None);
        Assert.Equal(4600m, updatedRequest!.Road!.DistanceMeters);
    }

    [Fact]
    public async Task SyncAsync_forwards_current_h10_state_to_graph_service()
    {
        var parcelId = Guid.NewGuid();
        var roadReferenceId = Guid.NewGuid();
        var waterReferenceId = Guid.NewGuid();
        var soilReferenceId = Guid.NewGuid();

        await using var dbContext = CreateDbContext(parcelId, roadReferenceId, waterReferenceId, soilReferenceId);
        var graphService = new RecordingGisKnowledgeGraphService();
        var service = new LandParcelGisKnowledgeGraphSyncService(
            dbContext,
            graphService,
            NullLogger<LandParcelGisKnowledgeGraphSyncService>.Instance);

        await service.SyncAsync(parcelId);

        Assert.Single(graphService.SyncRequests);
        Assert.Equal(parcelId, graphService.SyncRequests[0].ParcelId);
        Assert.Equal(roadReferenceId, graphService.SyncRequests[0].Road!.RoadReferenceId);
    }

    private static LandIntelligenceDbContext CreateDbContext(
        Guid parcelId,
        Guid roadReferenceId,
        Guid waterReferenceId,
        Guid soilReferenceId,
        bool unavailable = false)
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new LandIntelligenceDbContext(options);

        dbContext.LandParcels.Add(new LandParcelEntity
        {
            Id = parcelId,
            CadastralNumber = "H11-001",
            LandCategoryId = Guid.NewGuid(),
            AreaValue = 1m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Southern Province",
            District = "Hambantota",
            DivisionalSecretariat = "Hambantota DS",
            Centroid = new Point(81.12, 6.12) { SRID = 4326 }
        });

        dbContext.GisAdministrativeBoundaries.AddRange(
            new GisAdministrativeBoundaryEntity
            {
                Id = Guid.NewGuid(),
                Name = "Southern",
                BoundaryType = GisAdministrativeBoundaryType.Province,
                SourceName = "LandIntelligence_GIS",
                SourceLayer = "gis_administrative_boundaries",
                Boundary = CreateBoundary(80, 5, 82, 7),
                ImportedAt = DateTimeOffset.UtcNow
            },
            new GisAdministrativeBoundaryEntity
            {
                Id = Guid.NewGuid(),
                Name = "Hambantota",
                BoundaryType = GisAdministrativeBoundaryType.District,
                SourceName = "LandIntelligence_GIS",
                SourceLayer = "gis_administrative_boundaries",
                Boundary = CreateBoundary(80.5, 5.5, 81.5, 6.5),
                ImportedAt = DateTimeOffset.UtcNow
            });

        dbContext.GisRoads.Add(new GisRoadEntity
        {
            Id = roadReferenceId,
            Name = "Test Road",
            RoadType = GisRoadType.Primary,
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "gis_roads",
            Geometry = new MultiLineString([]),
            ImportedAt = DateTimeOffset.UtcNow
        });

        dbContext.GisWaterFeatures.Add(new GisWaterFeatureEntity
        {
            Id = waterReferenceId,
            Name = "Test Canal",
            FeatureType = GisWaterFeatureType.Canal,
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "gis_water_features",
            Geometry = new Point(81.11, 6.11),
            ImportedAt = DateTimeOffset.UtcNow
        });

        dbContext.GisSoilGroups.Add(new GisSoilGroupEntity
        {
            Id = soilReferenceId,
            Name = "Red Yellow Latosols",
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "gis_soil_groups",
            Boundary = CreateBoundary(80.5, 5.5, 81.5, 6.5),
            ImportedAt = DateTimeOffset.UtcNow
        });

        dbContext.LandParcelGisEnrichmentSnapshots.Add(new LandParcelGisEnrichmentSnapshotEntity
        {
            Id = GisDerivedIntelligenceIdentity.CreateSnapshotId(parcelId),
            LandParcelId = parcelId,
            OverallStatus = unavailable
                ? LandParcelGisEnrichmentOverallStatus.Unavailable
                : LandParcelGisEnrichmentOverallStatus.Partial,
            AdministrativeStatus = unavailable ? null : AdministrativeLocationVerificationStatus.Verified,
            DetectedProvince = unavailable ? null : "Southern",
            DetectedDistrict = unavailable ? null : "Hambantota",
            SourceName = "LandIntelligence_GIS",
            EnrichedAt = DateTimeOffset.UtcNow
        });

        if (!unavailable)
        {
            dbContext.InfrastructureFeatures.AddRange(
                new InfrastructureFeatureEntity
                {
                    Id = GisDerivedIntelligenceIdentity.CreateRoadInfrastructureId(parcelId, roadReferenceId),
                    LandParcelId = parcelId,
                    Type = InfrastructureFeatureType.Road,
                    Name = "Test Road",
                    DistanceMeters = 4100m,
                    GisReferenceId = roadReferenceId,
                    DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                        new AttributeProvenance(
                            AttributeProvenanceSourceType.Derived,
                            GisDerivedIntelligenceOwnership.SourceName,
                            1m,
                            DateTimeOffset.UtcNow,
                            false))
                },
                new InfrastructureFeatureEntity
                {
                    Id = GisDerivedIntelligenceIdentity.CreateWaterInfrastructureId(parcelId, waterReferenceId),
                    LandParcelId = parcelId,
                    Type = InfrastructureFeatureType.Other,
                    Name = "Test Canal",
                    DistanceMeters = 250m,
                    GisReferenceId = waterReferenceId,
                    Description = "[GIS-DERIVED] Natural water proximity",
                    DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                        new AttributeProvenance(
                            AttributeProvenanceSourceType.Derived,
                            GisDerivedIntelligenceOwnership.SourceName,
                            1m,
                            DateTimeOffset.UtcNow,
                            false))
                });

            dbContext.ParcelDerivedSoilGroups.Add(new ParcelDerivedSoilGroupEntity
            {
                Id = GisDerivedIntelligenceIdentity.CreateSoilGroupRecordId(parcelId),
                LandParcelId = parcelId,
                SoilGroupReferenceId = soilReferenceId,
                SoilGroupName = "Red Yellow Latosols",
                OverlapPercentage = 92.5m,
                SourceName = "LandIntelligence_GIS",
                SourceLayer = "gis_soil_groups",
                ProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                    new AttributeProvenance(
                        AttributeProvenanceSourceType.Derived,
                        GisDerivedIntelligenceOwnership.SourceName,
                        1m,
                        DateTimeOffset.UtcNow,
                        false))!,
                DerivedAt = DateTimeOffset.UtcNow
            });
        }

        dbContext.SaveChanges();
        return dbContext;
    }

    private static MultiPolygon CreateBoundary(double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon(new LinearRing([
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY)
        ]));

        return new MultiPolygon(new[] { polygon });
    }

    private sealed class RecordingGisKnowledgeGraphService : NoOpKnowledgeGraphService
    {
        public List<GisDerivedParcelIntelligenceGraphSyncRequest> SyncRequests { get; } = [];

        public override Task SyncGisDerivedParcelIntelligenceAsync(
            GisDerivedParcelIntelligenceGraphSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            SyncRequests.Add(request);
            return Task.CompletedTask;
        }
    }

    private class NoOpKnowledgeGraphService : IKnowledgeGraphService
    {
        public virtual Task SyncGisDerivedParcelIntelligenceAsync(
            GisDerivedParcelIntelligenceGraphSyncRequest request,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertLandParcelAsync(LandParcelGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertAdministrativeAreaAsync(AdministrativeAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertLandCategoryAsync(LandCategoryGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertLandUseAsync(LandUseGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertSpatialConstraintAsync(SpatialConstraintGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertRegulationAsync(RegulationGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertInfrastructureFeatureAsync(InfrastructureFeatureGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertEnvironmentalAreaAsync(EnvironmentalAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToAdministrativeAreaAsync(Guid parcelId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToCategoryAsync(Guid parcelId, Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToUseAsync(Guid parcelId, Guid landUseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToSpatialConstraintAsync(Guid parcelId, Guid constraintId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToRegulationAsync(Guid parcelId, Guid regulationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToInfrastructureFeatureAsync(Guid parcelId, Guid featureId, decimal? distanceMeters = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToEnvironmentalAreaAsync(Guid parcelId, Guid environmentalAreaId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncLandParcelGraphAsync(LandParcel parcel, Guid categoryId, Guid? landUseId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LandRelationshipDto>>([]);

        public Task<GraphTraversalResultDto> TraverseFromParcelAsync(Guid parcelId, int maxDepth = 2, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<LandParcelGisGraphIntelligenceDto?>(null);

        public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(Guid soilGroupReferenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
