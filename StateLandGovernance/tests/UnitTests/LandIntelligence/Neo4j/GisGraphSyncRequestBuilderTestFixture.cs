using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

internal static class GisGraphSyncRequestBuilderTestFixture
{
    internal static LandIntelligenceDbContext CreateDbContext(
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
}
