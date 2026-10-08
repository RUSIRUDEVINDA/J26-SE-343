using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class GisGraphSyncRequestBuilderTests
{
    [Fact]
    public async Task BuildSyncRequestAsync_reads_h10_persisted_state_without_rerunning_enrichment()
    {
        var parcelId = Guid.NewGuid();
        var roadReferenceId = Guid.NewGuid();
        var waterReferenceId = Guid.NewGuid();
        var soilReferenceId = Guid.NewGuid();

        await using var dbContext = GisGraphSyncRequestBuilderTestFixture.CreateDbContext(
            parcelId,
            roadReferenceId,
            waterReferenceId,
            soilReferenceId);
        var builder = new GisGraphSyncRequestBuilder(dbContext);

        var request = await builder.BuildSyncRequestAsync(parcelId, CancellationToken.None);

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
        await using var dbContext = GisGraphSyncRequestBuilderTestFixture.CreateDbContext(
            parcelId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            unavailable: true);
        var builder = new GisGraphSyncRequestBuilder(dbContext);

        var request = await builder.BuildSyncRequestAsync(parcelId, CancellationToken.None);

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

        await using var dbContext = GisGraphSyncRequestBuilderTestFixture.CreateDbContext(
            parcelId,
            roadReferenceId,
            waterReferenceId,
            soilReferenceId);
        var builder = new GisGraphSyncRequestBuilder(dbContext);

        var initialRequest = await builder.BuildSyncRequestAsync(parcelId, CancellationToken.None);
        Assert.Equal(4100m, initialRequest!.Road!.DistanceMeters);

        var roadFeature = await dbContext.InfrastructureFeatures
            .FirstAsync(feature => feature.LandParcelId == parcelId && feature.Type == InfrastructureFeatureType.Road);
        roadFeature.DistanceMeters = 4600m;
        await dbContext.SaveChangesAsync();

        var updatedRequest = await builder.BuildSyncRequestAsync(parcelId, CancellationToken.None);
        Assert.Equal(4600m, updatedRequest!.Road!.DistanceMeters);
    }
}
