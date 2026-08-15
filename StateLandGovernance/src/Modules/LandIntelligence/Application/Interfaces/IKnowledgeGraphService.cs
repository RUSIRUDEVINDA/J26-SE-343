using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Knowledge graph contract for Component 1 land relationship representation (Neo4j-backed).
/// Stores relationship references only — PostgreSQL/PostGIS remains the system of record.
/// </summary>
public interface IKnowledgeGraphService
{
    Task UpsertLandParcelAsync(
        LandParcelGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertAdministrativeAreaAsync(
        AdministrativeAreaGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertLandCategoryAsync(
        LandCategoryGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertLandUseAsync(
        LandUseGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertSpatialConstraintAsync(
        SpatialConstraintGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertRegulationAsync(
        RegulationGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertInfrastructureFeatureAsync(
        InfrastructureFeatureGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task UpsertEnvironmentalAreaAsync(
        EnvironmentalAreaGraphNodeDto node,
        CancellationToken cancellationToken = default);

    Task LinkParcelToAdministrativeAreaAsync(
        Guid parcelId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default);

    Task LinkParcelToCategoryAsync(
        Guid parcelId,
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task LinkParcelToUseAsync(
        Guid parcelId,
        Guid landUseId,
        CancellationToken cancellationToken = default);

    Task LinkParcelToSpatialConstraintAsync(
        Guid parcelId,
        Guid constraintId,
        CancellationToken cancellationToken = default);

    Task LinkParcelToRegulationAsync(
        Guid parcelId,
        Guid regulationId,
        CancellationToken cancellationToken = default);

    Task LinkParcelToInfrastructureFeatureAsync(
        Guid parcelId,
        Guid featureId,
        decimal? distanceMeters = null,
        CancellationToken cancellationToken = default);

    Task LinkParcelToEnvironmentalAreaAsync(
        Guid parcelId,
        Guid environmentalAreaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a domain parcel and its owned relationships into the knowledge graph.
    /// </summary>
    Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        Guid categoryId,
        Guid? landUseId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);

    Task<GraphTraversalResultDto> TraverseFromParcelAsync(
        Guid parcelId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task DeleteLandParcelGraphAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
