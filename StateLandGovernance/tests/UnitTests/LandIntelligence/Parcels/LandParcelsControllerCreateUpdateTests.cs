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

public sealed class LandParcelsControllerCreateUpdateTests
{
    private readonly LandParcelsController _controller;
    private readonly LandParcel _seedParcel;

    public LandParcelsControllerCreateUpdateTests()
    {
        _seedParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-CONTROLLER-001");
        var repository = new InMemoryLandParcelRepository(_seedParcel);

        _controller = new LandParcelsController(
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
                new UpdateLandParcelCommandValidator()));
    }

    [Fact]
    public async Task CreateParcelAsync_returns_created_with_location_header()
    {
        var request = new CreateLandParcelRequest
        {
            CadastralNumber = "SYNTH-CONTROLLER-CREATE-001",
            SurveyPlanReference = "SYNTH-SURVEY-002",
            CategoryType = LandCategoryType.StateLand,
            CategoryDescription = "[SYNTHETIC] Controller create test",
            AreaValue = 3m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Western",
            District = "Colombo",
            DivisionalSecretariat = "Colombo DS",
            GramaNiladhariDivision = "GN-Controller",
            CentroidLatitude = 6.9271,
            CentroidLongitude = 79.8612,
            CurrentUseType = LandUseType.Agricultural,
            CurrentUseDescription = "[SYNTHETIC] Agricultural use"
        };

        var response = await _controller.CreateParcelAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(response.Result);
        var parcel = Assert.IsType<LandParcelDto>(created.Value);
        Assert.Equal($"/api/v1/land/parcels/{parcel.Id}", created.Location);
        Assert.Equal("SYNTH-CONTROLLER-CREATE-001", parcel.Identifier.CadastralNumber);
    }

    [Fact]
    public async Task CreateParcelAsync_throws_validation_exception_for_invalid_request()
    {
        var request = new CreateLandParcelRequest
        {
            CadastralNumber = " ",
            AreaValue = 0m,
            Province = "Western",
            District = "Colombo",
            DivisionalSecretariat = "Colombo DS"
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateParcelAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateParcelAsync_returns_ok_for_existing_parcel()
    {
        var request = new UpdateLandParcelRequest
        {
            CurrentUseType = LandUseType.Industrial,
            CurrentUseDescription = "[SYNTHETIC] Industrial use"
        };

        var response = await _controller.UpdateParcelAsync(_seedParcel.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var parcel = Assert.IsType<LandParcelDto>(ok.Value);
        Assert.Equal(LandUseType.Industrial, parcel.CurrentUse?.Type);
    }

    [Fact]
    public async Task UpdateParcelAsync_throws_validation_exception_for_invalid_request()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.UpdateParcelAsync(_seedParcel.Id, new UpdateLandParcelRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateParcelAsync_throws_not_found_for_missing_parcel()
    {
        var request = new UpdateLandParcelRequest
        {
            CurrentUseType = LandUseType.Residential
        };

        await Assert.ThrowsAsync<LandParcelNotFoundException>(() =>
            _controller.UpdateParcelAsync(Guid.NewGuid(), request, CancellationToken.None));
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

        public Task SyncLandParcelGraphAsync(
            LandParcel parcel,
            CancellationToken cancellationToken = default) =>
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
    }
}
