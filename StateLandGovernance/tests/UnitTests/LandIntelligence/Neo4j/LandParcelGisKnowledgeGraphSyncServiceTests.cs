using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class LandParcelGisKnowledgeGraphSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_forwards_current_h10_state_to_graph_service()
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
        var graphService = new RecordingGisKnowledgeGraphService();
        var builder = new GisGraphSyncRequestBuilder(dbContext);
        var service = new LandParcelGisKnowledgeGraphSyncService(
            builder,
            graphService,
            NullLogger<LandParcelGisKnowledgeGraphSyncService>.Instance);

        await service.SyncAsync(parcelId);

        Assert.Single(graphService.SyncRequests);
        Assert.Equal(parcelId, graphService.SyncRequests[0].ParcelId);
        Assert.Equal(roadReferenceId, graphService.SyncRequests[0].Road!.RoadReferenceId);
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
