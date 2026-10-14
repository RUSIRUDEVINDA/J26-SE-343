using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.UnitTests.LandIntelligence.Persistence;

public sealed class LandParcelCentroidPersistenceTests
{
    [Fact]
    public void ApplyFullUpdate_persists_centroid_change()
    {
        var category = new LandCategoryEntity
        {
            Id = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001"),
            Type = LandCategoryType.StateLand,
            Name = "State Land",
            Description = "test"
        };
        var entity = new LandParcelEntity
        {
            Id = Guid.NewGuid(),
            CadastralNumber = "SYNTH-CENTROID-001",
            LandCategoryId = category.Id,
            LandCategory = category,
            AreaValue = 1m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Western",
            District = "Colombo",
            DivisionalSecretariat = "Colombo",
            Centroid = NtsGeometryServices.Instance.CreateGeometryFactory(4326)
                .CreatePoint(new Coordinate(79.8612, 6.9271)),
            SpatialReferenceSystemId = 4326
        };

        var parcel = LandParcelPersistenceMapper.ToDomain(entity);
        parcel.UpdateSpatial(new SpatialReference(6.94, 79.87, "EPSG:4326"));

        LandParcelPersistenceMapper.ApplyFullUpdate(entity, parcel);

        Assert.Equal(79.87, entity.Centroid.X, precision: 6);
        Assert.Equal(6.94, entity.Centroid.Y, precision: 6);
    }

    [Fact]
    public async Task Repository_update_then_fresh_context_reload_sees_new_centroid()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var parcel = new LandParcel(
            new ParcelIdentifier($"SYNTH-C-{Guid.NewGuid():N}"[..20], "PLAN-1"),
            new LandCategory(LandCategoryType.StateLand, "test"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"));

        await using (var createContext = CreateContext(databaseName))
        {
            SeedLookups(createContext);
            var repository = new LandParcelRepository(createContext);
            await repository.AddAsync(parcel);
        }

        await using (var updateContext = CreateContext(databaseName))
        {
            var repository = new LandParcelRepository(updateContext);
            var loaded = await repository.GetByIdAsync(parcel.Id);
            Assert.NotNull(loaded);
            loaded!.UpdateSpatial(new SpatialReference(6.935, 79.87, "EPSG:4326"));
            await repository.UpdateAsync(loaded);
        }

        await using var reloadContext = CreateContext(databaseName);
        var reloaded = await new LandParcelRepository(reloadContext).GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(6.935, reloaded!.Spatial.CentroidLatitude, precision: 6);
        Assert.Equal(79.87, reloaded.Spatial.CentroidLongitude, precision: 6);

        var entity = await reloadContext.LandParcels.AsNoTracking().SingleAsync(p => p.Id == parcel.Id);
        Assert.Equal(79.87, entity.Centroid.X, precision: 6);
        Assert.Equal(6.935, entity.Centroid.Y, precision: 6);
    }

    [Fact]
    public async Task InvalidateLocationDependentEvidence_marks_snapshot_unavailable_and_clears_gis_children()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var parcelId = Guid.NewGuid();

        await using (var context = CreateContext(databaseName))
        {
            SeedLookups(context);
            context.LandParcels.Add(new LandParcelEntity
            {
                Id = parcelId,
                CadastralNumber = "SYNTH-INV-001",
                LandCategoryId = LandIntelligenceSeedData.StateLandCategoryId,
                AreaValue = 1m,
                AreaUnit = AreaUnit.Hectares,
                Province = "Western",
                District = "Colombo",
                DivisionalSecretariat = "Colombo",
                Centroid = NtsGeometryServices.Instance.CreateGeometryFactory(4326)
                    .CreatePoint(new Coordinate(79.8612, 6.9271)),
                SpatialReferenceSystemId = 4326
            });
            context.LandParcelGisEnrichmentSnapshots.Add(new LandParcelGisEnrichmentSnapshotEntity
            {
                Id = Guid.NewGuid(),
                LandParcelId = parcelId,
                OverallStatus = LandParcelGisEnrichmentOverallStatus.Complete,
                SourceName = GisDerivedIntelligenceOwnership.SourceName,
                EnrichedAt = DateTimeOffset.UtcNow
            });
            context.InfrastructureFeatures.Add(new InfrastructureFeatureEntity
            {
                Id = Guid.NewGuid(),
                LandParcelId = parcelId,
                Type = InfrastructureFeatureType.Road,
                Name = "GIS road",
                DistanceMeters = 12,
                DistanceProvenanceJson = AttributeProvenancePersistenceMapper.Serialize(
                    AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName)),
                SpatialReferenceSystemId = 4326
            });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(databaseName))
        {
            var service = new LandParcelGisEnrichmentPersistenceService(
                context,
                NullLogger<LandParcelGisEnrichmentPersistenceService>.Instance);
            await service.InvalidateLocationDependentEvidenceAsync(parcelId);
        }

        await using var reload = CreateContext(databaseName);
        var snapshot = await reload.LandParcelGisEnrichmentSnapshots.SingleAsync(s => s.LandParcelId == parcelId);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, snapshot.OverallStatus);
        Assert.Empty(await reload.InfrastructureFeatures.Where(f => f.LandParcelId == parcelId).ToListAsync());
    }

    private static LandIntelligenceDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LandIntelligenceDbContext(options);
    }

    private static void SeedLookups(LandIntelligenceDbContext context)
    {
        if (context.LandCategories.Any())
        {
            return;
        }

        foreach (var category in LandIntelligenceSeedData.Categories)
        {
            context.LandCategories.Add(category);
        }

        foreach (var landUse in LandIntelligenceSeedData.LandUses)
        {
            context.LandUses.Add(landUse);
        }

        context.SaveChanges();
    }
}
