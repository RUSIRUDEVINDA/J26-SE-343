using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

/// <summary>
/// Placeholder when Neo4j environment variables are not configured.
/// </summary>
internal sealed class UnconfiguredKnowledgeGraphService : IKnowledgeGraphService
{
    private static ServiceConfigurationException CreateException() =>
        new(
            $"Neo4j is not configured. Set {Neo4jSettings.ConnectionEnvironmentVariable}, " +
            $"{Neo4jSettings.UsernameEnvironmentVariable}, and {Neo4jSettings.PasswordEnvironmentVariable} in .env.");

    public Task UpsertLandParcelAsync(LandParcelGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertAdministrativeAreaAsync(AdministrativeAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertLandCategoryAsync(LandCategoryGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertLandUseAsync(LandUseGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertSpatialConstraintAsync(SpatialConstraintGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertRegulationAsync(RegulationGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertInfrastructureFeatureAsync(InfrastructureFeatureGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task UpsertEnvironmentalAreaAsync(EnvironmentalAreaGraphNodeDto node, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToAdministrativeAreaAsync(Guid parcelId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToCategoryAsync(Guid parcelId, Guid categoryId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToUseAsync(Guid parcelId, Guid landUseId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToSpatialConstraintAsync(Guid parcelId, Guid constraintId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToRegulationAsync(Guid parcelId, Guid regulationId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToInfrastructureFeatureAsync(Guid parcelId, Guid featureId, decimal? distanceMeters = null, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task LinkParcelToEnvironmentalAreaAsync(Guid parcelId, Guid environmentalAreaId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task SyncLandParcelGraphAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task SyncLandParcelGraphAsync(LandParcel parcel, Guid categoryId, Guid? landUseId, Guid administrativeAreaId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task<GraphTraversalResultDto> TraverseFromParcelAsync(Guid parcelId, int maxDepth = 2, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task DeleteLandParcelGraphAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task SyncGisDerivedParcelIntelligenceAsync(
        GisDerivedParcelIntelligenceGraphSyncRequest request,
        CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default) =>
        throw CreateException();

    public Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
        Guid soilGroupReferenceId,
        CancellationToken cancellationToken = default) =>
        throw CreateException();
}
