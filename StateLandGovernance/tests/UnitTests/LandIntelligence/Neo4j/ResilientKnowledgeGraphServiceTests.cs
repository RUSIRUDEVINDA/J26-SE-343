using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class ResilientKnowledgeGraphServiceTests
{
    [Fact]
    public async Task GetRelationshipsAsync_returns_postgis_baseline_when_neo4j_fails()
    {
        var parcelId = Guid.NewGuid();
        var baseline = new[]
        {
            new LandRelationshipDto(
                "LOCATED_IN",
                "LandParcel",
                parcelId.ToString(),
                "District",
                Guid.NewGuid().ToString(),
                "Hambantota")
        };

        var service = new ResilientKnowledgeGraphService(
            new FailingNeo4jKnowledgeGraphService(new ServiceUnavailableException("Neo4j unavailable")),
            new FixedBaselineProvider(baselineRelationships: baseline),
            NullLogger<ResilientKnowledgeGraphService>.Instance);

        var relationships = await service.GetRelationshipsAsync(parcelId);

        Assert.Equal(baseline, relationships);
    }

    [Fact]
    public async Task GetRelationshipsAsync_returns_postgis_baseline_when_located_in_conflicts()
    {
        var parcelId = Guid.NewGuid();
        var baselineDistrictId = Guid.NewGuid();
        var neo4jDistrictId = Guid.NewGuid();

        var baseline = new[]
        {
            new LandRelationshipDto(
                "LOCATED_IN",
                "LandParcel",
                parcelId.ToString(),
                "District",
                baselineDistrictId.ToString(),
                "Hambantota")
        };
        var neo4j = new[]
        {
            new LandRelationshipDto(
                "LOCATED_IN",
                "LandParcel",
                parcelId.ToString(),
                "District",
                neo4jDistrictId.ToString(),
                "Colombo")
        };

        var service = new ResilientKnowledgeGraphService(
            new FixedNeo4jKnowledgeGraphService(relationships: neo4j),
            new FixedBaselineProvider(baselineRelationships: baseline),
            NullLogger<ResilientKnowledgeGraphService>.Instance);

        var relationships = await service.GetRelationshipsAsync(parcelId);

        Assert.Equal(baseline, relationships);
    }

    [Fact]
    public async Task GetParcelGisGraphIntelligenceAsync_returns_postgis_baseline_when_neo4j_conflicts()
    {
        var parcelId = Guid.NewGuid();
        var baseline = CreateIntelligence(parcelId, "Southern", "Hambantota");
        var neo4j = CreateIntelligence(parcelId, "Western", "Colombo");

        var service = new ResilientKnowledgeGraphService(
            new FixedNeo4jKnowledgeGraphService(gisIntelligence: neo4j),
            new FixedBaselineProvider(baselineIntelligence: baseline),
            NullLogger<ResilientKnowledgeGraphService>.Instance);

        var intelligence = await service.GetParcelGisGraphIntelligenceAsync(parcelId);

        Assert.Equal(baseline, intelligence);
    }

    private static LandParcelGisGraphIntelligenceDto CreateIntelligence(
        Guid parcelId,
        string province,
        string district) =>
        new()
        {
            ParcelId = parcelId,
            DetectedProvince = province,
            ProvinceReferenceId = Guid.NewGuid(),
            DetectedDistrict = district,
            DistrictReferenceId = Guid.NewGuid(),
            ConservationAreas = []
        };

    private sealed class FixedBaselineProvider : IPostGisKnowledgeGraphBaselineProvider
    {
        private readonly IReadOnlyList<LandRelationshipDto>? _baselineRelationships;
        private readonly LandParcelGisGraphIntelligenceDto? _baselineIntelligence;

        public FixedBaselineProvider(
            IReadOnlyList<LandRelationshipDto>? baselineRelationships = null,
            LandParcelGisGraphIntelligenceDto? baselineIntelligence = null)
        {
            _baselineRelationships = baselineRelationships;
            _baselineIntelligence = baselineIntelligence;
        }

        public Task<LandParcelGisGraphIntelligenceDto?> GetGisGraphIntelligenceAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_baselineIntelligence);

        public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LandRelationshipDto>>(_baselineRelationships ?? []);

        public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
            Guid soilGroupReferenceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class FixedNeo4jKnowledgeGraphService : INeo4jKnowledgeGraphService
    {
        private readonly IReadOnlyList<LandRelationshipDto>? _relationships;
        private readonly LandParcelGisGraphIntelligenceDto? _gisIntelligence;

        public FixedNeo4jKnowledgeGraphService(
            IReadOnlyList<LandRelationshipDto>? relationships = null,
            LandParcelGisGraphIntelligenceDto? gisIntelligence = null)
        {
            _relationships = relationships;
            _gisIntelligence = gisIntelligence;
        }

        public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LandRelationshipDto>>(_relationships ?? []);

        public Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_gisIntelligence);

        public Task UpsertLandParcelAsync(LandParcelGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertAdministrativeAreaAsync(AdministrativeAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertLandCategoryAsync(LandCategoryGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertLandUseAsync(LandUseGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertSpatialConstraintAsync(SpatialConstraintGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertRegulationAsync(RegulationGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertInfrastructureFeatureAsync(InfrastructureFeatureGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertEnvironmentalAreaAsync(EnvironmentalAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToAdministrativeAreaAsync(Guid parcelId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToCategoryAsync(Guid parcelId, Guid categoryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToUseAsync(Guid parcelId, Guid landUseId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToSpatialConstraintAsync(Guid parcelId, Guid constraintId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToRegulationAsync(Guid parcelId, Guid regulationId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToInfrastructureFeatureAsync(Guid parcelId, Guid featureId, decimal? distanceMeters = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToEnvironmentalAreaAsync(Guid parcelId, Guid environmentalAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncLandParcelGraphAsync(LandParcel parcel, Guid categoryId, Guid? landUseId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GraphTraversalResultDto> TraverseFromParcelAsync(Guid parcelId, int maxDepth = 2, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncGisDerivedParcelIntelligenceAsync(GisDerivedParcelIntelligenceGraphSyncRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(Guid soilGroupReferenceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FailingNeo4jKnowledgeGraphService(Exception exception) : INeo4jKnowledgeGraphService
    {
        public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
            Guid parcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<LandRelationshipDto>>(exception);

        public Task UpsertLandParcelAsync(LandParcelGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertAdministrativeAreaAsync(AdministrativeAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertLandCategoryAsync(LandCategoryGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertLandUseAsync(LandUseGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertSpatialConstraintAsync(SpatialConstraintGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertRegulationAsync(RegulationGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertInfrastructureFeatureAsync(InfrastructureFeatureGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpsertEnvironmentalAreaAsync(EnvironmentalAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToAdministrativeAreaAsync(Guid parcelId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToCategoryAsync(Guid parcelId, Guid categoryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToUseAsync(Guid parcelId, Guid landUseId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToSpatialConstraintAsync(Guid parcelId, Guid constraintId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToRegulationAsync(Guid parcelId, Guid regulationId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToInfrastructureFeatureAsync(Guid parcelId, Guid featureId, decimal? distanceMeters = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkParcelToEnvironmentalAreaAsync(Guid parcelId, Guid environmentalAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncLandParcelGraphAsync(LandParcel parcel, Guid categoryId, Guid? landUseId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GraphTraversalResultDto> TraverseFromParcelAsync(Guid parcelId, int maxDepth = 2, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SyncGisDerivedParcelIntelligenceAsync(GisDerivedParcelIntelligenceGraphSyncRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(Guid soilGroupReferenceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
