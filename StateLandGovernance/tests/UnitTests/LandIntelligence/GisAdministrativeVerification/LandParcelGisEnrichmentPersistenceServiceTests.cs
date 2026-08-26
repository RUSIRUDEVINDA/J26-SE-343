using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class LandParcelGisEnrichmentPersistenceServiceTests
{
    [Fact]
    public async Task PersistAsync_does_not_modify_official_parcel_values()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId, soilType: "Official Survey Loam");
        var service = CreateService(dbContext);

        var result = CreatePartialResult(parcelId);
        await service.PersistAsync(result);

        var parcel = await dbContext.LandParcels.SingleAsync(entity => entity.Id == parcelId);
        Assert.Equal("Southern Province", parcel.Province);
        Assert.Equal("Hambantota", parcel.District);
        Assert.Equal("Official Survey Loam", parcel.SoilType);
    }

    [Fact]
    public async Task PersistAsync_is_idempotent_for_repeated_runs()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);
        var result = CreatePartialResult(parcelId);

        await service.PersistAsync(result);
        await service.PersistAsync(result);

        var roadCount = await dbContext.InfrastructureFeatures
            .CountAsync(feature =>
                feature.LandParcelId == parcelId
                && feature.Type == InfrastructureFeatureType.Road);
        var waterCount = await dbContext.InfrastructureFeatures
            .CountAsync(feature =>
                feature.LandParcelId == parcelId
                && feature.Type == InfrastructureFeatureType.Other);
        var soilCount = await dbContext.ParcelDerivedSoilGroups.CountAsync(entity => entity.LandParcelId == parcelId);
        var conservationCount = await dbContext.EnvironmentalRestrictions
            .CountAsync(restriction => restriction.LandParcelId == parcelId);
        var snapshotCount = await dbContext.LandParcelGisEnrichmentSnapshots
            .CountAsync(snapshot => snapshot.LandParcelId == parcelId);

        Assert.Equal(1, roadCount);
        Assert.Equal(1, waterCount);
        Assert.Equal(1, soilCount);
        Assert.Equal(1, conservationCount);
        Assert.Equal(1, snapshotCount);
    }

    [Fact]
    public async Task PersistAsync_refreshes_gis_owned_derived_values()
    {
        var parcelId = Guid.NewGuid();
        var roadId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);

        var first = CreatePartialResult(parcelId, roadId: roadId, roadDistanceMeters: 4200d);
        await service.PersistAsync(first);

        var second = CreatePartialResult(parcelId, roadId: roadId, roadDistanceMeters: 3800d);
        await service.PersistAsync(second);

        var roadFeature = await dbContext.InfrastructureFeatures.SingleAsync(feature =>
            feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Road);

        Assert.Equal(3800m, roadFeature.DistanceMeters);
    }

    [Fact]
    public async Task PersistAsync_persists_partial_result_without_fabricated_erosion_records()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);

        await service.PersistAsync(CreatePartialResult(parcelId, includeConservation: true));

        var restrictions = await dbContext.EnvironmentalRestrictions
            .Where(restriction => restriction.LandParcelId == parcelId)
            .ToListAsync();

        Assert.Single(restrictions);
        Assert.Equal(EnvironmentalRestrictionType.ProtectedArea, restrictions[0].Type);
        Assert.DoesNotContain(
            restrictions,
            restriction => restriction.Description.Contains("erosion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PersistAsync_does_not_fabricate_spatial_intelligence_when_unavailable()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);

        await service.PersistAsync(CreateUnavailableResult(parcelId));

        Assert.Empty(await dbContext.InfrastructureFeatures
            .Where(feature => feature.LandParcelId == parcelId)
            .ToListAsync());
        Assert.Empty(await dbContext.ParcelDerivedSoilGroups
            .Where(entity => entity.LandParcelId == parcelId)
            .ToListAsync());
        Assert.Empty(await dbContext.EnvironmentalRestrictions
            .Where(restriction => restriction.LandParcelId == parcelId)
            .ToListAsync());

        var snapshot = await dbContext.LandParcelGisEnrichmentSnapshots
            .SingleAsync(entity => entity.LandParcelId == parcelId);
        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, snapshot.OverallStatus);
    }

    [Fact]
    public async Task PersistAsync_retains_provenance_on_derived_records()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);
        var completedAt = DateTimeOffset.Parse("2026-08-26T08:00:00Z");

        await service.PersistAsync(CreatePartialResult(parcelId, completedAt: completedAt));

        var road = await dbContext.InfrastructureFeatures.SingleAsync(feature =>
            feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Road);
        var water = await dbContext.InfrastructureFeatures.SingleAsync(feature =>
            feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Other);
        var soil = await dbContext.ParcelDerivedSoilGroups.SingleAsync(entity => entity.LandParcelId == parcelId);
        var conservation = await dbContext.EnvironmentalRestrictions.SingleAsync(restriction =>
            restriction.LandParcelId == parcelId);

        AssertDerivedProvenance(road.DistanceProvenanceJson, completedAt);
        AssertDerivedProvenance(water.DistanceProvenanceJson, completedAt);
        AssertDerivedProvenance(soil.ProvenanceJson, completedAt);
        AssertDerivedProvenance(conservation.DataProvenanceJson, completedAt);
        Assert.Equal(GisReferenceDataPaths.SourceName, soil.SourceName);
        Assert.Equal("gis_soil_groups", soil.SourceLayer);
    }

    [Fact]
    public async Task PersistAsync_does_not_delete_manual_or_non_gis_records()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        dbContext.InfrastructureFeatures.Add(new InfrastructureFeatureEntity
        {
            Id = Guid.NewGuid(),
            LandParcelId = parcelId,
            Type = InfrastructureFeatureType.WaterSupply,
            Name = "Manual utility supply",
            DistanceMeters = 500m,
            Description = "[MANUAL] Utility water supply evidence"
        });
        dbContext.EnvironmentalRestrictions.Add(new EnvironmentalRestrictionEntity
        {
            Id = Guid.NewGuid(),
            LandParcelId = parcelId,
            Type = EnvironmentalRestrictionType.Wetland,
            Description = "[MANUAL] Wetland restriction",
            Severity = RestrictionSeverity.High
        });
        dbContext.SpatialConstraints.Add(new SpatialConstraintEntity
        {
            Id = Guid.NewGuid(),
            LandParcelId = parcelId,
            Type = SpatialConstraintType.BufferZone,
            Description = "[MANUAL] Flood zone",
            Severity = RestrictionSeverity.Medium
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.PersistAsync(CreatePartialResult(parcelId, includeConservation: true));

        Assert.Contains(
            await dbContext.InfrastructureFeatures.Where(feature => feature.LandParcelId == parcelId).ToListAsync(),
            feature => feature.Type == InfrastructureFeatureType.WaterSupply);
        Assert.Contains(
            await dbContext.EnvironmentalRestrictions.Where(restriction => restriction.LandParcelId == parcelId)
                .ToListAsync(),
            restriction => restriction.Type == EnvironmentalRestrictionType.Wetland);
        Assert.Contains(
            await dbContext.SpatialConstraints.Where(constraint => constraint.LandParcelId == parcelId).ToListAsync(),
            constraint => constraint.Type == SpatialConstraintType.BufferZone);
    }

    [Fact]
    public async Task PersistAsync_uses_other_infrastructure_type_for_natural_water_not_water_supply()
    {
        var parcelId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(parcelId);
        var service = CreateService(dbContext);

        await service.PersistAsync(CreatePartialResult(parcelId));

        var water = await dbContext.InfrastructureFeatures.SingleAsync(feature =>
            feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Other);

        Assert.Contains("Natural water proximity", water.Description, StringComparison.Ordinal);
        Assert.Contains("not utility water supply", water.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static LandParcelGisEnrichmentPersistenceService CreateService(LandIntelligenceDbContext dbContext) =>
        new(dbContext, NullLogger<LandParcelGisEnrichmentPersistenceService>.Instance);

    private static LandIntelligenceDbContext CreateDbContext(Guid parcelId, string? soilType = null)
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new LandIntelligenceDbContext(options);
        dbContext.LandParcels.Add(new LandParcelEntity
        {
            Id = parcelId,
            CadastralNumber = $"H10-{parcelId:N}"[..20],
            LandCategoryId = Guid.NewGuid(),
            AreaValue = 1m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Southern Province",
            District = "Hambantota",
            DivisionalSecretariat = "Hambantota DS",
            SoilType = soilType,
            Centroid = new Point(81.12, 6.12) { SRID = 4326 }
        });
        dbContext.SaveChanges();
        return dbContext;
    }

    private static LandParcelGisEnrichmentResult CreatePartialResult(
        Guid parcelId,
        Guid? roadId = null,
        double? roadDistanceMeters = 4100d,
        bool includeConservation = true,
        DateTimeOffset? completedAt = null)
    {
        roadId ??= Guid.NewGuid();
        var waterId = Guid.NewGuid();
        var soilGroupId = Guid.NewGuid();
        var conservationId = Guid.NewGuid();
        var derivedAt = completedAt ?? DateTimeOffset.UtcNow;

        return new LandParcelGisEnrichmentResult
        {
            ParcelId = parcelId,
            CadastralNumber = "HAM-H10-001",
            OverallStatus = LandParcelGisEnrichmentOverallStatus.Partial,
            GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
            Administrative = new AdministrativeLocationVerificationResult
            {
                ParcelId = parcelId,
                StoredProvince = "Southern Province",
                StoredDistrict = "Hambantota",
                DetectedProvince = "Southern",
                DetectedDistrict = "Hambantota",
                ProvinceMatches = true,
                DistrictMatches = true,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_administrative_boundaries",
                Status = AdministrativeLocationVerificationStatus.Verified,
                Evidence = ["Verified against GIS boundaries."],
                StoredProvinceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Official,
                    "Land Commissioner",
                    1m,
                    derivedAt,
                    true),
                StoredDistrictProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Official,
                    "Land Commissioner",
                    1m,
                    derivedAt,
                    true),
                DetectedProvinceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false),
                DetectedDistrictProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false),
                BoundarySourceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false)
            },
            RoadAccessibility = new RoadAccessibilityEnrichmentResult
            {
                ParcelId = parcelId,
                RoadId = roadId,
                RoadName = "Test Road",
                RoadType = GisReferenceRoadType.Primary,
                DistanceMeters = roadDistanceMeters,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                Status = RoadAccessibilityEnrichmentStatus.Available,
                Evidence = ["Nearest mapped road found."],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_roads",
                DistanceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Derived,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false)
            },
            WaterProximity = new WaterProximityEnrichmentResult
            {
                ParcelId = parcelId,
                FeatureId = waterId,
                FeatureName = "Test Canal",
                FeatureType = GisReferenceWaterFeatureType.Canal,
                DistanceMeters = 250d,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                Status = WaterProximityEnrichmentStatus.Available,
                Evidence = ["Nearest mapped water feature found."],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_water_features",
                DistanceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Derived,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false)
            },
            Soil = new SoilGroupEnrichmentResult
            {
                ParcelId = parcelId,
                StoredSoilType = "Official Survey Loam",
                OfficialSoilTypePreserved = true,
                PrimarySoilGroup = "Red Yellow Latosols",
                PrimarySoilGroupId = soilGroupId,
                OverlapPercentage = 92.5m,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                Status = SoilGroupEnrichmentStatus.Available,
                Evidence = ["Primary soil group overlap calculated."],
                Overlaps =
                [
                    new SoilGroupOverlapEvidence
                    {
                        SoilGroupId = soilGroupId,
                        SoilGroupName = "Red Yellow Latosols",
                        OverlapAreaSquareMeters = 9200d,
                        OverlapPercentage = 92.5m
                    }
                ],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_soil_groups",
                DerivedSoilGroupProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Derived,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false)
            },
            Environmental = new EnvironmentalSpatialConstraintEnrichmentResult
            {
                ParcelId = parcelId,
                Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                GeometryBasis = AdministrativeLocationGeometryBasis.Boundary,
                IntersectsSoilConservationArea = includeConservation,
                ConservationAreas = includeConservation
                    ?
                    [
                        new SoilConservationAreaEvidence
                        {
                            Id = conservationId,
                            Name = "Pilot Conservation Area",
                            OverlapPercentage = 12.5m,
                            SourceName = GisReferenceDataPaths.SourceName,
                            SourceLayer = "gis_soil_conservation_areas"
                        }
                    ]
                    : [],
                ErosionDataStatus = ErosionDataStatus.Unavailable,
                ErosionObservations = [],
                Evidence = ["Conservation intersection evaluated.", "Erosion data unavailable."],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_soil_conservation_areas",
                DerivedConservationProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Derived,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    derivedAt,
                    false)
            },
            Evidence = ["Unified enrichment completed."],
            Warnings = ["Soil erosion GIS observations are unavailable."],
            Failures = [],
            CompletedAt = derivedAt
        };
    }

    private static LandParcelGisEnrichmentResult CreateUnavailableResult(Guid parcelId) =>
        new()
        {
            ParcelId = parcelId,
            CadastralNumber = "OUT-H10-001",
            OverallStatus = LandParcelGisEnrichmentOverallStatus.Unavailable,
            Administrative = new AdministrativeLocationVerificationResult
            {
                ParcelId = parcelId,
                StoredProvince = "Western Province",
                StoredDistrict = "Colombo",
                DetectedProvince = null,
                DetectedDistrict = null,
                ProvinceMatches = null,
                DistrictMatches = null,
                GeometryBasis = null,
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_administrative_boundaries",
                Status = AdministrativeLocationVerificationStatus.Unavailable,
                Evidence = ["Outside GIS coverage."],
                StoredProvinceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Official,
                    "Land Commissioner",
                    1m,
                    DateTimeOffset.UtcNow,
                    true),
                StoredDistrictProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Official,
                    "Land Commissioner",
                    1m,
                    DateTimeOffset.UtcNow,
                    true),
                BoundarySourceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    GisReferenceDataPaths.SourceName,
                    1m,
                    DateTimeOffset.UtcNow,
                    false)
            },
            RoadAccessibility = new RoadAccessibilityEnrichmentResult
            {
                ParcelId = parcelId,
                Status = RoadAccessibilityEnrichmentStatus.Unavailable,
                Evidence = [],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_roads"
            },
            WaterProximity = new WaterProximityEnrichmentResult
            {
                ParcelId = parcelId,
                Status = WaterProximityEnrichmentStatus.Unavailable,
                Evidence = [],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_water_features"
            },
            Soil = new SoilGroupEnrichmentResult
            {
                ParcelId = parcelId,
                OfficialSoilTypePreserved = true,
                Status = SoilGroupEnrichmentStatus.Unavailable,
                Evidence = [],
                Overlaps = [],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_soil_groups"
            },
            Environmental = new EnvironmentalSpatialConstraintEnrichmentResult
            {
                ParcelId = parcelId,
                Status = EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable,
                IntersectsSoilConservationArea = false,
                ConservationAreas = [],
                ErosionDataStatus = ErosionDataStatus.Unavailable,
                ErosionObservations = [],
                Evidence = [],
                SourceName = GisReferenceDataPaths.SourceName,
                SourceLayer = "gis_soil_conservation_areas"
            },
            Evidence = [],
            Warnings = [],
            Failures = [],
            CompletedAt = DateTimeOffset.UtcNow
        };

    private static void AssertDerivedProvenance(string? json, DateTimeOffset expectedCollectedAt)
    {
        var provenance = AttributeProvenancePersistenceMapper.Deserialize(json);
        Assert.NotNull(provenance);
        Assert.Equal(AttributeProvenanceSourceType.Derived, provenance!.SourceType);
        Assert.Equal(GisDerivedIntelligenceOwnership.SourceName, provenance.SourceName);
        Assert.Equal(expectedCollectedAt, provenance.CollectedAt);
    }
}
