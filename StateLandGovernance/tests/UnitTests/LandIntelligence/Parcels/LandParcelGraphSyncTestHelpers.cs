using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

internal sealed class RecordingLandParcelGraphSynchronizer : ILandParcelGraphSynchronizer
{
    public int SyncCallCount { get; private set; }
    public LandParcel? LastParcel { get; private set; }

    public Task SynchronizeAfterPersistAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        SyncCallCount++;
        LastParcel = parcel;
        return Task.CompletedTask;
    }
}

internal sealed class NoOpLandParcelGraphSynchronizer : ILandParcelGraphSynchronizer
{
    public Task SynchronizeAfterPersistAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class FailingLandParcelRepository : ILandParcelRepository
{
    private readonly InMemoryLandParcelRepository _inner = new();

    public FailingLandParcelRepository(params LandParcel[] seedParcels)
    {
        foreach (var parcel in seedParcels)
        {
            _inner.AddAsync(parcel).GetAwaiter().GetResult();
        }
    }

    public Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<LandParcel?> GetByCadastralNumberAsync(string cadastralNumber, CancellationToken cancellationToken = default) =>
        _inner.GetByCadastralNumberAsync(cadastralNumber, cancellationToken);

    public Task<IReadOnlyList<LandParcel>> SearchAsync(LandSearchRequest request, CancellationToken cancellationToken = default) =>
        _inner.SearchAsync(request, cancellationToken);

    public Task<int> CountSearchAsync(LandSearchRequest request, CancellationToken cancellationToken = default) =>
        _inner.CountSearchAsync(request, cancellationToken);

    public Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Simulated PostgreSQL persistence failure.");

    public Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Simulated PostgreSQL persistence failure.");
}

internal sealed class RecordingKnowledgeGraphService : IKnowledgeGraphService
{
    public int SyncCallCount { get; private set; }
    public LandParcel? LastSyncedParcel { get; private set; }
    public bool ThrowServiceConfigurationOnSync { get; set; }

    public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        SyncCallCount++;
        LastSyncedParcel = parcel;

        if (ThrowServiceConfigurationOnSync)
        {
            throw new ServiceConfigurationException("Neo4j is not configured.");
        }

        return Task.CompletedTask;
    }

    public Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        Guid categoryId,
        Guid? landUseId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default) =>
        SyncLandParcelGraphAsync(parcel, cancellationToken);

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

    public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LandRelationshipDto>>([]);

    public Task<GraphTraversalResultDto> TraverseFromParcelAsync(Guid parcelId, int maxDepth = 2, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GraphTraversalResultDto(parcelId, maxDepth, [], []));

    public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
