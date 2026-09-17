using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class LandParcelGisEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichAsync_preserves_successful_sections_when_environmental_fails()
    {
        var parcel = CreateParcel("HAM-001");
        await using var dbContext = CreateDbContext(parcel);

        var service = new LandParcelGisEnrichmentService(
            dbContext,
            new FakeAdministrativeService(CreateAdministrative(parcel.Id)),
            new FakeRoadService(CreateRoad(parcel.Id)),
            new FakeWaterService(CreateWater(parcel.Id)),
            new FakeSoilService(CreateSoil(parcel.Id)),
            new ThrowingEnvironmentalService(),
            NullLogger<LandParcelGisEnrichmentService>.Instance);

        var result = await service.EnrichAsync(parcel.Id);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, result.OverallStatus);
        Assert.NotNull(result.Administrative);
        Assert.NotNull(result.RoadAccessibility);
        Assert.NotNull(result.WaterProximity);
        Assert.NotNull(result.Soil);
        Assert.Null(result.Environmental);
        Assert.Single(result.Failures);
        Assert.Equal("Environmental", result.Failures[0].Section);
        Assert.Contains(result.Warnings, warning => warning.Contains("Environmental enrichment failed"));
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_when_administrative_is_unavailable()
    {
        var parcel = CreateParcel("OUT-001");
        await using var dbContext = CreateDbContext(parcel);

        var service = new LandParcelGisEnrichmentService(
            dbContext,
            new FakeAdministrativeService(CreateAdministrative(parcel.Id, unavailable: true)),
            new FakeRoadService(CreateRoad(parcel.Id, unavailable: true)),
            new FakeWaterService(CreateWater(parcel.Id, unavailable: true)),
            new FakeSoilService(CreateSoil(parcel.Id, unavailable: true)),
            new FakeEnvironmentalService(CreateEnvironmental(parcel.Id, unavailable: true)),
            NullLogger<LandParcelGisEnrichmentService>.Instance);

        var result = await service.EnrichAsync(parcel.Id);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, result.OverallStatus);
    }

    private static LandIntelligenceDbContext CreateDbContext(LandParcel parcel)
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new LandIntelligenceDbContext(options);
        dbContext.LandParcels.Add(new LandParcelEntity
        {
            Id = parcel.Id,
            CadastralNumber = parcel.Identifier.CadastralNumber,
            LandCategoryId = Guid.NewGuid(),
            AreaValue = 1m,
            AreaUnit = AreaUnit.Hectares,
            Province = parcel.Location.Province,
            District = parcel.Location.District,
            DivisionalSecretariat = parcel.Location.DivisionalSecretariat,
            Centroid = new Point(parcel.Spatial.CentroidLongitude, parcel.Spatial.CentroidLatitude)
            {
                SRID = 4326
            }
        });
        dbContext.SaveChanges();
        return dbContext;
    }

    private static LandParcel CreateParcel(string cadastralNumber) =>
        new(
            new ParcelIdentifier(cadastralNumber, "PLAN"),
            new LandCategory(LandCategoryType.StateLand, "Test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(6.12, 81.12));

    private static AdministrativeLocationVerificationResult CreateAdministrative(
        Guid parcelId,
        bool unavailable = false) =>
        new()
        {
            ParcelId = parcelId,
            StoredProvince = "Southern Province",
            DetectedProvince = unavailable ? null : "Southern",
            ProvinceMatches = !unavailable,
            StoredDistrict = "Hambantota",
            DetectedDistrict = unavailable ? null : "Hambantota",
            DistrictMatches = !unavailable,
            GeometryBasis = unavailable ? null : AdministrativeLocationGeometryBasis.Centroid,
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "district_boundaries",
            Status = unavailable
                ? AdministrativeLocationVerificationStatus.Unavailable
                : AdministrativeLocationVerificationStatus.Verified,
            Evidence = [],
            StoredProvinceProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.Official, "Parcel", 1m, DateTimeOffset.UtcNow, true),
            StoredDistrictProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.Official, "Parcel", 1m, DateTimeOffset.UtcNow, true),
            BoundarySourceProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.ExternalAuthoritative, "GIS", 1m, DateTimeOffset.UtcNow, true)
        };

    private static RoadAccessibilityEnrichmentResult CreateRoad(
        Guid parcelId,
        bool unavailable = false) =>
        new()
        {
            ParcelId = parcelId,
            RoadId = unavailable ? null : Guid.NewGuid(),
            DistanceMeters = unavailable ? null : 1000d,
            GeometryBasis = unavailable ? null : AdministrativeLocationGeometryBasis.Centroid,
            Status = unavailable
                ? RoadAccessibilityEnrichmentStatus.Unavailable
                : RoadAccessibilityEnrichmentStatus.Available,
            Evidence = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "expressways"
        };

    private static WaterProximityEnrichmentResult CreateWater(
        Guid parcelId,
        bool unavailable = false) =>
        new()
        {
            ParcelId = parcelId,
            FeatureId = unavailable ? null : Guid.NewGuid(),
            DistanceMeters = unavailable ? null : 500d,
            GeometryBasis = unavailable ? null : AdministrativeLocationGeometryBasis.Centroid,
            Status = unavailable
                ? WaterProximityEnrichmentStatus.Unavailable
                : WaterProximityEnrichmentStatus.Available,
            Evidence = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "canals"
        };

    private static SoilGroupEnrichmentResult CreateSoil(
        Guid parcelId,
        bool unavailable = false) =>
        new()
        {
            ParcelId = parcelId,
            OfficialSoilTypePreserved = true,
            PrimarySoilGroup = unavailable ? null : "Red Yellow Latosols",
            GeometryBasis = unavailable ? null : AdministrativeLocationGeometryBasis.Centroid,
            Status = unavailable
                ? SoilGroupEnrichmentStatus.Unavailable
                : SoilGroupEnrichmentStatus.Available,
            Evidence = [],
            Overlaps = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_groups"
        };

    private static EnvironmentalSpatialConstraintEnrichmentResult CreateEnvironmental(
        Guid parcelId,
        bool unavailable = false) =>
        new()
        {
            ParcelId = parcelId,
            Status = unavailable
                ? EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable
                : EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            GeometryBasis = unavailable ? null : AdministrativeLocationGeometryBasis.Centroid,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = ErosionDataStatus.Unavailable,
            ErosionObservations = [],
            Evidence = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas, soil_erosion"
        };

    private sealed class FakeAdministrativeService(AdministrativeLocationVerificationResult result)
        : IAdministrativeLocationVerificationService
    {
        public Task<AdministrativeLocationVerificationResult> VerifyAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeRoadService(RoadAccessibilityEnrichmentResult result)
        : IRoadAccessibilityEnrichmentService
    {
        public Task<RoadAccessibilityEnrichmentResult> EnrichAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeWaterService(WaterProximityEnrichmentResult result)
        : IWaterProximityEnrichmentService
    {
        public Task<WaterProximityEnrichmentResult> EnrichAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeSoilService(SoilGroupEnrichmentResult result) : ISoilGroupEnrichmentService
    {
        public Task<SoilGroupEnrichmentResult> EnrichAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeEnvironmentalService(EnvironmentalSpatialConstraintEnrichmentResult result)
        : IEnvironmentalSpatialConstraintEnrichmentService
    {
        public Task<EnvironmentalSpatialConstraintEnrichmentResult> EnrichAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingEnvironmentalService : IEnvironmentalSpatialConstraintEnrichmentService
    {
        public Task<EnvironmentalSpatialConstraintEnrichmentResult> EnrichAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated environmental enrichment failure.");
    }
}
