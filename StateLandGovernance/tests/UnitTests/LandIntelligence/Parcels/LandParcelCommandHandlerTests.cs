using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

public sealed class LandParcelCommandHandlerTests
{
    [Fact]
    public async Task CreateLandParcelCommandHandler_creates_valid_parcel()
    {
        var repository = new InMemoryLandParcelRepository();
        var handler = new CreateLandParcelCommandHandler(
            repository,
            new NoOpLandParcelGraphSynchronizer(),
            new CreateLandParcelCommandValidator());

        var result = await handler.HandleAsync(CreateValidCreateCommand());

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("SYNTH-CREATE-001", result.Identifier.CadastralNumber);
        Assert.Equal(LandCategoryType.StateLand, result.Category.Type);
        Assert.Equal(LandUseType.Agricultural, result.CurrentUse?.Type);
        Assert.Equal(2.5m, result.Area.Value);
    }

    [Fact]
    public async Task CreateLandParcelCommandHandler_throws_for_invalid_parcel()
    {
        var handler = new CreateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new NoOpLandParcelGraphSynchronizer(),
            new CreateLandParcelCommandValidator());

        var invalidCommand = CreateValidCreateCommand() with { CadastralNumber = "  " };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(invalidCommand));

        Assert.Contains(exception.Errors, error => error.Contains("Cadastral number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_updates_existing_parcel()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-UPDATE-001");
        var repository = new InMemoryLandParcelRepository(existing);
        var handler = new UpdateLandParcelCommandHandler(
            repository,
            new NoOpLandParcelGraphSynchronizer(),
            new UpdateLandParcelCommandValidator());

        var result = await handler.HandleAsync(new UpdateLandParcelCommand(
            existing.Id,
            LandUseType.Commercial,
            "[SYNTHETIC] Updated commercial use",
            "Clay",
            "Undulating terrain",
            25m));

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(LandUseType.Commercial, result.CurrentUse?.Type);
        Assert.Equal("Clay", result.Characteristics?.SoilType);
        Assert.Equal("Undulating terrain", result.Characteristics?.TerrainDescription);
        Assert.Equal(25m, result.Characteristics?.ElevationMeters);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_throws_for_invalid_update()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-UPDATE-002");
        var handler = new UpdateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(existing),
            new NoOpLandParcelGraphSynchronizer(),
            new UpdateLandParcelCommandValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new UpdateLandParcelCommand(existing.Id, null, null, null, null, null)));

        Assert.Contains(exception.Errors, error => error.Contains("At least one updatable field", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_throws_when_parcel_not_found()
    {
        var handler = new UpdateLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new NoOpLandParcelGraphSynchronizer(),
            new UpdateLandParcelCommandValidator());

        var missingId = Guid.NewGuid();

        await Assert.ThrowsAsync<LandParcelNotFoundException>(() =>
            handler.HandleAsync(new UpdateLandParcelCommand(
                missingId,
                LandUseType.Residential,
                "[SYNTHETIC] Residential use",
                null,
                null,
                null)));
    }

    [Fact]
    public async Task CreateLandParcelCommandHandler_synchronizes_graph_after_successful_persist()
    {
        var repository = new InMemoryLandParcelRepository();
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new CreateLandParcelCommandHandler(
            repository,
            graphSynchronizer,
            new CreateLandParcelCommandValidator());

        var result = await handler.HandleAsync(CreateValidCreateCommand("SYNTH-GRAPH-CREATE-001"));

        Assert.Equal(1, graphSynchronizer.SyncCallCount);
        Assert.NotNull(graphSynchronizer.LastParcel);
        Assert.Equal(result.Id, graphSynchronizer.LastParcel!.Id);
        Assert.Equal(LandUseType.Agricultural, graphSynchronizer.LastParcel.CurrentUse?.Type);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_synchronizes_graph_with_latest_parcel_state()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-GRAPH-UPDATE-001");
        var repository = new InMemoryLandParcelRepository(existing);
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new UpdateLandParcelCommandHandler(
            repository,
            graphSynchronizer,
            new UpdateLandParcelCommandValidator());

        var result = await handler.HandleAsync(new UpdateLandParcelCommand(
            existing.Id,
            LandUseType.Commercial,
            "[SYNTHETIC] Updated commercial use",
            null,
            null,
            null));

        Assert.Equal(1, graphSynchronizer.SyncCallCount);
        Assert.NotNull(graphSynchronizer.LastParcel);
        Assert.Equal(result.Id, graphSynchronizer.LastParcel!.Id);
        Assert.Equal(LandUseType.Commercial, graphSynchronizer.LastParcel.CurrentUse?.Type);
    }

    [Fact]
    public async Task CreateLandParcelCommandHandler_does_not_sync_graph_when_persist_fails()
    {
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new CreateLandParcelCommandHandler(
            new FailingLandParcelRepository(),
            graphSynchronizer,
            new CreateLandParcelCommandValidator());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateValidCreateCommand("SYNTH-GRAPH-FAIL-CREATE")));

        Assert.Equal(0, graphSynchronizer.SyncCallCount);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_does_not_sync_graph_when_persist_fails()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-GRAPH-FAIL-UPDATE");
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new UpdateLandParcelCommandHandler(
            new FailingLandParcelRepository(existing),
            graphSynchronizer,
            new UpdateLandParcelCommandValidator());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdateLandParcelCommand(
                existing.Id,
                LandUseType.Industrial,
                "[SYNTHETIC] Industrial use",
                null,
                null,
                null)));

        Assert.Equal(0, graphSynchronizer.SyncCallCount);
    }

    [Fact]
    public async Task CreateLandParcelCommandHandler_returns_created_parcel_when_graph_sync_is_unavailable()
    {
        var repository = new InMemoryLandParcelRepository();
        var knowledgeGraph = new RecordingKnowledgeGraphService { ThrowServiceConfigurationOnSync = true };
        var synchronizer = new LandParcelGraphSynchronizer(
            knowledgeGraph,
            NullLogger<LandParcelGraphSynchronizer>.Instance);
        var handler = new CreateLandParcelCommandHandler(
            repository,
            synchronizer,
            new CreateLandParcelCommandValidator());

        var result = await handler.HandleAsync(CreateValidCreateCommand("SYNTH-GRAPH-UNAVAIL-CREATE"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(1, knowledgeGraph.SyncCallCount);
        var persisted = await repository.SearchAsync(new LandSearchRequest());
        Assert.Single(persisted);
    }

    [Fact]
    public async Task UpdateLandParcelCommandHandler_returns_updated_parcel_when_graph_sync_is_unavailable()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-GRAPH-UNAVAIL-UPDATE");
        var repository = new InMemoryLandParcelRepository(existing);
        var knowledgeGraph = new RecordingKnowledgeGraphService { ThrowServiceConfigurationOnSync = true };
        var synchronizer = new LandParcelGraphSynchronizer(
            knowledgeGraph,
            NullLogger<LandParcelGraphSynchronizer>.Instance);
        var handler = new UpdateLandParcelCommandHandler(
            repository,
            synchronizer,
            new UpdateLandParcelCommandValidator());

        var result = await handler.HandleAsync(new UpdateLandParcelCommand(
            existing.Id,
            LandUseType.Tourism,
            "[SYNTHETIC] Tourism use",
            null,
            null,
            null));

        Assert.Equal(LandUseType.Tourism, result.CurrentUse?.Type);
        Assert.Equal(1, knowledgeGraph.SyncCallCount);
    }

    [Fact]
    public async Task SyncLandParcelGraphAsync_can_be_called_repeatedly_for_same_parcel()
    {
        var knowledgeGraph = new RecordingKnowledgeGraphService();
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-GRAPH-IDEMPOTENT");

        await knowledgeGraph.SyncLandParcelGraphAsync(parcel);
        await knowledgeGraph.SyncLandParcelGraphAsync(parcel);

        Assert.Equal(2, knowledgeGraph.SyncCallCount);
        Assert.Equal(parcel.Id, knowledgeGraph.LastSyncedParcel?.Id);
    }

    [Fact]
    public async Task DeleteLandParcelCommandHandler_deletes_existing_parcel()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-DELETE-001");
        var repository = new InMemoryLandParcelRepository(existing);
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new DeleteLandParcelCommandHandler(
            repository,
            graphSynchronizer,
            new DeleteLandParcelCommandValidator());

        await handler.HandleAsync(new DeleteLandParcelCommand(existing.Id));

        Assert.Empty(await repository.SearchAsync(new LandSearchRequest()));
        Assert.Equal(1, graphSynchronizer.RemoveCallCount);
        Assert.Equal(existing.Id, graphSynchronizer.LastRemovedParcelId);
    }

    [Fact]
    public async Task DeleteLandParcelCommandHandler_throws_when_parcel_not_found()
    {
        var handler = new DeleteLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new NoOpLandParcelGraphSynchronizer(),
            new DeleteLandParcelCommandValidator());

        var missingId = Guid.NewGuid();

        await Assert.ThrowsAsync<LandParcelNotFoundException>(() =>
            handler.HandleAsync(new DeleteLandParcelCommand(missingId)));
    }

    [Fact]
    public async Task DeleteLandParcelCommandHandler_throws_for_empty_identifier()
    {
        var handler = new DeleteLandParcelCommandHandler(
            new InMemoryLandParcelRepository(),
            new NoOpLandParcelGraphSynchronizer(),
            new DeleteLandParcelCommandValidator());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new DeleteLandParcelCommand(Guid.Empty)));

        Assert.Contains(exception.Errors, error => error.Contains("Land parcel identifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteLandParcelCommandHandler_does_not_remove_graph_when_db_delete_fails()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-DELETE-FAIL");
        var graphSynchronizer = new RecordingLandParcelGraphSynchronizer();
        var handler = new DeleteLandParcelCommandHandler(
            new FailingDeleteLandParcelRepository(existing),
            graphSynchronizer,
            new DeleteLandParcelCommandValidator());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new DeleteLandParcelCommand(existing.Id)));

        Assert.Equal(0, graphSynchronizer.RemoveCallCount);
    }

    [Fact]
    public async Task DeleteLandParcelCommandHandler_completes_when_graph_delete_is_unavailable()
    {
        var existing = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-DELETE-GRAPH-UNAVAIL");
        var repository = new InMemoryLandParcelRepository(existing);
        var knowledgeGraph = new RecordingKnowledgeGraphService { ThrowServiceConfigurationOnDelete = true };
        var synchronizer = new LandParcelGraphSynchronizer(
            knowledgeGraph,
            NullLogger<LandParcelGraphSynchronizer>.Instance);
        var handler = new DeleteLandParcelCommandHandler(
            repository,
            synchronizer,
            new DeleteLandParcelCommandValidator());

        await handler.HandleAsync(new DeleteLandParcelCommand(existing.Id));

        Assert.Empty(await repository.SearchAsync(new LandSearchRequest()));
        Assert.Equal(1, knowledgeGraph.DeleteCallCount);
        Assert.Equal(existing.Id, knowledgeGraph.LastDeletedParcelId);
    }

    [Fact]
    public async Task RemoveAfterDeleteAsync_calls_delete_land_parcel_graph_once()
    {
        var parcelId = Guid.NewGuid();
        var knowledgeGraph = new RecordingKnowledgeGraphService();
        var synchronizer = new LandParcelGraphSynchronizer(
            knowledgeGraph,
            NullLogger<LandParcelGraphSynchronizer>.Instance);

        await synchronizer.RemoveAfterDeleteAsync(parcelId);

        Assert.Equal(1, knowledgeGraph.DeleteCallCount);
        Assert.Equal(parcelId, knowledgeGraph.LastDeletedParcelId);
    }

    [Fact]
    public async Task RemoveAfterDeleteAsync_swallows_neo4j_unavailability()
    {
        var parcelId = Guid.NewGuid();
        var knowledgeGraph = new RecordingKnowledgeGraphService { ThrowNeo4jExceptionOnDelete = true };
        var synchronizer = new LandParcelGraphSynchronizer(
            knowledgeGraph,
            NullLogger<LandParcelGraphSynchronizer>.Instance);

        await synchronizer.RemoveAfterDeleteAsync(parcelId);

        Assert.Equal(1, knowledgeGraph.DeleteCallCount);
    }

    private static CreateLandParcelCommand CreateValidCreateCommand(string cadastralNumber = "SYNTH-CREATE-001") =>
        new(
            cadastralNumber,
            "SYNTH-SURVEY-001",
            LandCategoryType.StateLand,
            "[SYNTHETIC] Test parcel",
            2.5m,
            AreaUnit.Hectares,
            "Western",
            "Colombo",
            "Colombo DS",
            "GN-Test",
            6.9271,
            79.8612,
            "EPSG:4326",
            null,
            LandUseType.Agricultural,
            "[SYNTHETIC] Agricultural use",
            "Red Yellow Latosol",
            "Flat terrain",
            12m);
}
