using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

/// <summary>
/// Wraps Neo4j semantic reads with PostGIS-backed fallback when queries fail,
/// time out, or conflict with authoritative spatial persistence.
/// </summary>
internal sealed class ResilientKnowledgeGraphService : IKnowledgeGraphService
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);

    private readonly INeo4jKnowledgeGraphService _neo4j;
    private readonly IPostGisKnowledgeGraphBaselineProvider _baselineProvider;
    private readonly ILogger<ResilientKnowledgeGraphService> _logger;

    public ResilientKnowledgeGraphService(
        INeo4jKnowledgeGraphService neo4j,
        IPostGisKnowledgeGraphBaselineProvider baselineProvider,
        ILogger<ResilientKnowledgeGraphService> logger)
    {
        _neo4j = neo4j;
        _baselineProvider = baselineProvider;
        _logger = logger;
    }

    public Task UpsertLandParcelAsync(
        LandParcelGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertLandParcelAsync(node, cancellationToken);

    public Task UpsertAdministrativeAreaAsync(
        AdministrativeAreaGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertAdministrativeAreaAsync(node, cancellationToken);

    public Task UpsertLandCategoryAsync(
        LandCategoryGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertLandCategoryAsync(node, cancellationToken);

    public Task UpsertLandUseAsync(
        LandUseGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertLandUseAsync(node, cancellationToken);

    public Task UpsertSpatialConstraintAsync(
        SpatialConstraintGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertSpatialConstraintAsync(node, cancellationToken);

    public Task UpsertRegulationAsync(
        RegulationGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertRegulationAsync(node, cancellationToken);

    public Task UpsertInfrastructureFeatureAsync(
        InfrastructureFeatureGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertInfrastructureFeatureAsync(node, cancellationToken);

    public Task UpsertEnvironmentalAreaAsync(
        EnvironmentalAreaGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        _neo4j.UpsertEnvironmentalAreaAsync(node, cancellationToken);

    public Task LinkParcelToAdministrativeAreaAsync(
        Guid parcelId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToAdministrativeAreaAsync(parcelId, administrativeAreaId, cancellationToken);

    public Task LinkParcelToCategoryAsync(
        Guid parcelId,
        Guid categoryId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToCategoryAsync(parcelId, categoryId, cancellationToken);

    public Task LinkParcelToUseAsync(
        Guid parcelId,
        Guid landUseId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToUseAsync(parcelId, landUseId, cancellationToken);

    public Task LinkParcelToSpatialConstraintAsync(
        Guid parcelId,
        Guid constraintId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToSpatialConstraintAsync(parcelId, constraintId, cancellationToken);

    public Task LinkParcelToRegulationAsync(
        Guid parcelId,
        Guid regulationId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToRegulationAsync(parcelId, regulationId, cancellationToken);

    public Task LinkParcelToInfrastructureFeatureAsync(
        Guid parcelId,
        Guid featureId,
        decimal? distanceMeters = null,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToInfrastructureFeatureAsync(
            parcelId,
            featureId,
            distanceMeters,
            cancellationToken);

    public Task LinkParcelToEnvironmentalAreaAsync(
        Guid parcelId,
        Guid environmentalAreaId,
        CancellationToken cancellationToken = default) =>
        _neo4j.LinkParcelToEnvironmentalAreaAsync(parcelId, environmentalAreaId, cancellationToken);

    public Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        CancellationToken cancellationToken = default) =>
        _neo4j.SyncLandParcelGraphAsync(parcel, cancellationToken);

    public Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        Guid categoryId,
        Guid? landUseId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default) =>
        _neo4j.SyncLandParcelGraphAsync(parcel, categoryId, landUseId, administrativeAreaId, cancellationToken);

    public async Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var baseline = await _baselineProvider.GetRelationshipsAsync(parcelId, cancellationToken);

        try
        {
            var neo4j = await ExecuteNeo4jReadAsync(
                token => _neo4j.GetRelationshipsAsync(parcelId, token),
                cancellationToken);

            if (KnowledgeGraphNeo4jResultValidator.HasRelationshipConflict(baseline, neo4j, out var reason))
            {
                LogDiscrepancy(parcelId, reason);
                return baseline;
            }

            return neo4j;
        }
        catch (Exception ex) when (IsNeo4jReadFailure(ex))
        {
            LogNeo4jFailure(parcelId, "GetRelationshipsAsync", ex);
            return baseline;
        }
    }

    public async Task<GraphTraversalResultDto> TraverseFromParcelAsync(
        Guid parcelId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        var baselineRelationships = await _baselineProvider.GetRelationshipsAsync(parcelId, cancellationToken);

        try
        {
            return await ExecuteNeo4jReadAsync(
                token => _neo4j.TraverseFromParcelAsync(parcelId, maxDepth, token),
                cancellationToken);
        }
        catch (Exception ex) when (IsNeo4jReadFailure(ex))
        {
            LogNeo4jFailure(parcelId, "TraverseFromParcelAsync", ex);
            return new GraphTraversalResultDto(
                parcelId,
                maxDepth,
                baselineRelationships,
                []);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var baseline = await _baselineProvider.GetParcelIdsByCategoryAsync(categoryId, cancellationToken);

        try
        {
            return await ExecuteNeo4jReadAsync(
                token => _neo4j.GetParcelIdsByCategoryAsync(categoryId, token),
                cancellationToken);
        }
        catch (Exception ex) when (IsNeo4jReadFailure(ex))
        {
            LogNeo4jFailure(null, "GetParcelIdsByCategoryAsync", ex);
            return baseline;
        }
    }

    public Task DeleteLandParcelGraphAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default) =>
        _neo4j.DeleteLandParcelGraphAsync(parcelId, cancellationToken);

    public Task SyncGisDerivedParcelIntelligenceAsync(
        GisDerivedParcelIntelligenceGraphSyncRequest request,
        CancellationToken cancellationToken = default) =>
        _neo4j.SyncGisDerivedParcelIntelligenceAsync(request, cancellationToken);

    public async Task<LandParcelGisGraphIntelligenceDto?> GetParcelGisGraphIntelligenceAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var baseline = await _baselineProvider.GetGisGraphIntelligenceAsync(parcelId, cancellationToken);

        try
        {
            var neo4j = await ExecuteNeo4jReadAsync(
                token => _neo4j.GetParcelGisGraphIntelligenceAsync(parcelId, token),
                cancellationToken);

            if (neo4j is null)
            {
                return baseline;
            }

            if (baseline is not null
                && KnowledgeGraphNeo4jResultValidator.HasGisIntelligenceConflict(baseline, neo4j, out var reason))
            {
                LogDiscrepancy(parcelId, reason);
                return baseline;
            }

            return neo4j;
        }
        catch (Exception ex) when (IsNeo4jReadFailure(ex))
        {
            LogNeo4jFailure(parcelId, "GetParcelGisGraphIntelligenceAsync", ex);
            return baseline;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetParcelIdsByDerivedSoilGroupAsync(
        Guid soilGroupReferenceId,
        CancellationToken cancellationToken = default)
    {
        var baseline = await _baselineProvider.GetParcelIdsByDerivedSoilGroupAsync(
            soilGroupReferenceId,
            cancellationToken);

        try
        {
            return await ExecuteNeo4jReadAsync(
                token => _neo4j.GetParcelIdsByDerivedSoilGroupAsync(soilGroupReferenceId, token),
                cancellationToken);
        }
        catch (Exception ex) when (IsNeo4jReadFailure(ex))
        {
            LogNeo4jFailure(null, "GetParcelIdsByDerivedSoilGroupAsync", ex);
            return baseline;
        }
    }

    private static async Task<T> ExecuteNeo4jReadAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(QueryTimeout);
        return await operation(timeoutSource.Token);
    }

    private static bool IsNeo4jReadFailure(Exception exception) =>
        exception is Neo4jException
            or OperationCanceledException
            or ServiceUnavailableException
            or TransientException
            or ClientException;

    private void LogDiscrepancy(Guid parcelId, string reason) =>
        _logger.LogWarning(
            "Neo4j semantic result conflicted with PostGIS baseline for parcel {ParcelId}: {Reason}. Returning PostGIS baseline.",
            parcelId,
            reason);

    private void LogNeo4jFailure(Guid? parcelId, string operation, Exception exception)
    {
        if (parcelId is null)
        {
            _logger.LogWarning(
                exception,
                "Neo4j {Operation} failed or timed out. Returning PostGIS baseline.",
                operation);
            return;
        }

        _logger.LogWarning(
            exception,
            "Neo4j {Operation} failed or timed out for parcel {ParcelId}. Returning PostGIS baseline.",
            operation,
            parcelId);
    }
}
