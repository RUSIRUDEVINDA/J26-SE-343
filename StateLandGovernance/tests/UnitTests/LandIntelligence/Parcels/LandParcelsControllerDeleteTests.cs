using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Presentation.Controllers;
using StateLandGovernance.LandIntelligence.Presentation.Models;
using StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

public sealed class LandParcelsControllerDeleteTests
{
    [Fact]
    public async Task DeleteParcelAsync_returns_no_content_for_existing_parcel()
    {
        var seedParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-CONTROLLER-DELETE-001");
        var repository = new InMemoryLandParcelRepository(seedParcel);
        var controller = CreateController(repository);

        var response = await controller.DeleteParcelAsync(seedParcel.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
        Assert.Empty(await repository.SearchAsync(new LandSearchRequest()));
    }

    [Fact]
    public async Task DeleteParcelAsync_throws_not_found_for_missing_parcel()
    {
        var controller = CreateController(new InMemoryLandParcelRepository());

        await Assert.ThrowsAsync<LandParcelNotFoundException>(() =>
            controller.DeleteParcelAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteParcelAsync_throws_validation_exception_for_empty_identifier()
    {
        var controller = CreateController(new InMemoryLandParcelRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.DeleteParcelAsync(Guid.Empty, CancellationToken.None));
    }

    private static LandParcelsController CreateController(InMemoryLandParcelRepository repository)
    {
        return new LandParcelsController(
            new SearchLandParcelsQueryHandler(repository, new LandSearchRequestValidator()),
            new GetLandParcelByIdQueryHandler(repository),
            new GetSpatialConstraintsByParcelIdQueryHandler(
                repository,
                new EmptySpatialConstraintRepository(),
                new FakeSpatialAnalysisService()),
            new GetLandRelationshipsQueryHandler(repository, new EmptyKnowledgeGraphService()),
            new CreateLandParcelCommandHandler(
                repository,
                new NoOpLandParcelGraphSynchronizer(),
                new CreateLandParcelCommandValidator()),
            new UpdateLandParcelCommandHandler(
                repository,
                new NoOpLandParcelGraphSynchronizer(),
                new UpdateLandParcelCommandValidator()),
            new DeleteLandParcelCommandHandler(
                repository,
                new RecordingLandParcelGraphSynchronizer(),
                new DeleteLandParcelCommandValidator()));
    }

    private sealed class EmptySpatialConstraintRepository : ISpatialConstraintRepository
    {
        public Task<IReadOnlyList<SpatialConstraint>> GetByParcelIdAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpatialConstraint>>([]);
    }

    private sealed class EmptyKnowledgeGraphService : IKnowledgeGraphService
    {
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

        public Task LinkParcelToInfrastructureFeatureAsync(
            Guid parcelId,
            Guid featureId,
            decimal? distanceMeters = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LinkParcelToEnvironmentalAreaAsync(Guid parcelId, Guid environmentalAreaId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncLandParcelGraphAsync(
            LandParcel parcel,
            Guid categoryId,
            Guid? landUseId,
            Guid administrativeAreaId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LandRelationshipDto>>([]);

        public Task<GraphTraversalResultDto> TraverseFromParcelAsync(
            Guid parcelId,
            int maxDepth = 2,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphTraversalResultDto(parcelId, maxDepth, [], []));

        public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncGisDerivedParcelIntelligenceAsync(
            GisDerivedParcelIntelligenceGraphSyncRequest request,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LandParcelGisGraphIntelligenceDto?>(null);

        public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
            Guid soilGroupReferenceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
