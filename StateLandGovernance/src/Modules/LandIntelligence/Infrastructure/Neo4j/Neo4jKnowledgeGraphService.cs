using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Cypher;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Mapping;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

public sealed class Neo4jKnowledgeGraphService : IKnowledgeGraphService, IAsyncDisposable
{
    private readonly IDriver _driver;

    public Neo4jKnowledgeGraphService(IDriver driver)
    {
        _driver = driver;
    }

    public Task UpsertLandParcelAsync(
        LandParcelGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertLandParcel,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertAdministrativeAreaAsync(
        AdministrativeAreaGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertAdministrativeArea,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertLandCategoryAsync(
        LandCategoryGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertLandCategory,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertLandUseAsync(
        LandUseGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertLandUse,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertSpatialConstraintAsync(
        SpatialConstraintGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertSpatialConstraint,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertRegulationAsync(
        RegulationGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertRegulation,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertInfrastructureFeatureAsync(
        InfrastructureFeatureGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertInfrastructureFeature,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task UpsertEnvironmentalAreaAsync(
        EnvironmentalAreaGraphNodeDto node,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.UpsertEnvironmentalArea,
            KnowledgeGraphMapper.ToParameters(node),
            cancellationToken);

    public Task LinkParcelToAdministrativeAreaAsync(
        Guid parcelId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToAdministrativeArea,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, administrativeAreaId),
            cancellationToken);

    public Task LinkParcelToCategoryAsync(
        Guid parcelId,
        Guid categoryId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToCategory,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, categoryId),
            cancellationToken);

    public Task LinkParcelToUseAsync(
        Guid parcelId,
        Guid landUseId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToUse,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, landUseId),
            cancellationToken);

    public Task LinkParcelToSpatialConstraintAsync(
        Guid parcelId,
        Guid constraintId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToSpatialConstraint,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, constraintId),
            cancellationToken);

    public Task LinkParcelToRegulationAsync(
        Guid parcelId,
        Guid regulationId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToRegulation,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, regulationId),
            cancellationToken);

    public Task LinkParcelToInfrastructureFeatureAsync(
        Guid parcelId,
        Guid featureId,
        decimal? distanceMeters = null,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToInfrastructureFeature,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, featureId, distanceMeters),
            cancellationToken);

    public Task LinkParcelToEnvironmentalAreaAsync(
        Guid parcelId,
        Guid environmentalAreaId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.LinkParcelToEnvironmentalArea,
            KnowledgeGraphMapper.ToLinkParameters(parcelId, environmentalAreaId),
            cancellationToken);

    public Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        CancellationToken cancellationToken = default) =>
        SyncLandParcelGraphAsync(
            parcel,
            KnowledgeGraphReferenceResolver.ResolveCategoryId(parcel),
            KnowledgeGraphReferenceResolver.ResolveLandUseId(parcel),
            KnowledgeGraphReferenceResolver.ResolveAdministrativeAreaId(parcel.Location),
            cancellationToken);

    public async Task SyncLandParcelGraphAsync(
        LandParcel parcel,
        Guid categoryId,
        Guid? landUseId,
        Guid administrativeAreaId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWriteAsync(
            KnowledgeGraphCypher.ClearParcelRelationships,
            new { parcelId = parcel.Id.ToString() },
            cancellationToken);

        await UpsertLandParcelAsync(KnowledgeGraphMapper.ToLandParcelNode(parcel), cancellationToken);
        await UpsertAdministrativeAreaAsync(
            KnowledgeGraphMapper.ToAdministrativeAreaNode(administrativeAreaId, parcel.Location),
            cancellationToken);
        await UpsertLandCategoryAsync(
            KnowledgeGraphMapper.ToLandCategoryNode(categoryId, parcel.Category),
            cancellationToken);

        await LinkParcelToAdministrativeAreaAsync(parcel.Id, administrativeAreaId, cancellationToken);
        await LinkParcelToCategoryAsync(parcel.Id, categoryId, cancellationToken);

        if (parcel.CurrentUse is not null && landUseId is not null)
        {
            await UpsertLandUseAsync(
                KnowledgeGraphMapper.ToLandUseNode(landUseId.Value, parcel.CurrentUse),
                cancellationToken);
            await LinkParcelToUseAsync(parcel.Id, landUseId.Value, cancellationToken);
        }

        foreach (var constraint in parcel.SpatialConstraints)
        {
            await UpsertSpatialConstraintAsync(
                KnowledgeGraphMapper.ToSpatialConstraintNode(constraint),
                cancellationToken);
            await LinkParcelToSpatialConstraintAsync(parcel.Id, constraint.Id, cancellationToken);
        }

        foreach (var reference in parcel.RegulatoryReferences)
        {
            await UpsertRegulationAsync(
                KnowledgeGraphMapper.ToRegulationNode(reference),
                cancellationToken);
            await LinkParcelToRegulationAsync(parcel.Id, reference.Id, cancellationToken);
        }

        foreach (var feature in parcel.InfrastructureFeatures)
        {
            await UpsertInfrastructureFeatureAsync(
                KnowledgeGraphMapper.ToInfrastructureFeatureNode(feature),
                cancellationToken);
            await LinkParcelToInfrastructureFeatureAsync(
                parcel.Id,
                feature.Id,
                feature.DistanceMeters,
                cancellationToken);
        }

        foreach (var restriction in parcel.EnvironmentalRestrictions)
        {
            await UpsertEnvironmentalAreaAsync(
                KnowledgeGraphMapper.ToEnvironmentalAreaNode(restriction),
                cancellationToken);
            await LinkParcelToEnvironmentalAreaAsync(parcel.Id, restriction.Id, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            KnowledgeGraphCypher.GetParcelRelationships,
            new { parcelId = parcelId.ToString() });

        var records = await cursor.ToListAsync();
        return records
            .Select(record => KnowledgeGraphMapper.ToRelationshipDto(
                record["relationshipType"].As<string>(),
                record["sourceNodeType"].As<string>(),
                record["sourceNodeId"].As<string>(),
                record["targetNodeType"].As<string>(),
                record["targetNodeId"].As<string>(),
                ReadOptionalString(record, "description")))
            .ToList();
    }

    public async Task<GraphTraversalResultDto> TraverseFromParcelAsync(
        Guid parcelId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Traversal depth must be between 1 and 5.");
        }

        var cypher = KnowledgeGraphCypher.BuildTraverseFromParcelQuery(maxDepth);

        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            cypher,
            new { parcelId = parcelId.ToString() });

        var records = await cursor.ToListAsync();
        var relationships = records
            .Select(record => KnowledgeGraphMapper.ToRelationshipDto(
                record["relationshipType"].As<string>(),
                record["sourceNodeType"].As<string>(),
                record["sourceNodeId"].As<string>(),
                record["targetNodeType"].As<string>(),
                record["targetNodeId"].As<string>(),
                ReadOptionalString(record, "description")))
            .DistinctBy(dto => $"{dto.RelationshipType}:{dto.SourceNodeId}:{dto.TargetNodeId}")
            .ToList();

        var relatedParcelIds = records
            .Select(record => Guid.Parse(record["relatedParcelId"].As<string>()))
            .Distinct()
            .ToList();

        return new GraphTraversalResultDto(parcelId, maxDepth, relationships, relatedParcelIds);
    }

    public async Task<IReadOnlyList<Guid>> GetParcelIdsByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            KnowledgeGraphCypher.GetParcelIdsByCategory,
            new { categoryId = categoryId.ToString() });

        var records = await cursor.ToListAsync();
        return records
            .Select(record => Guid.Parse(record["parcelId"].As<string>()))
            .ToList();
    }

    public Task DeleteLandParcelGraphAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            KnowledgeGraphCypher.DeleteLandParcelGraph,
            new { parcelId = parcelId.ToString() },
            cancellationToken);

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static string? ReadOptionalString(IRecord record, string key)
    {
        if (!record.ContainsKey(key))
        {
            return null;
        }

        var value = record[key];
        return value switch
        {
            null => null,
            string text => text,
            _ => value.As<string>()
        };
    }

    private async Task ExecuteWriteAsync(
        string cypher,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypher, parameters);
            return 0;
        });
    }
}
