using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class Neo4jKnowledgeGraphIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private IKnowledgeGraphService? _knowledgeGraphService;
    private Guid _parcelId;

    public Task InitializeAsync()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return Task.CompletedTask;
        }

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(LandIntelligenceIntegrationConfiguration.LoadApiConfiguration());

        _serviceProvider = services.BuildServiceProvider();
        _knowledgeGraphService = _serviceProvider.GetRequiredService<IKnowledgeGraphService>();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Can_sync_synthetic_parcel_and_query_relationships()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_knowledgeGraphService);

        var parcel = CreateSyntheticParcel();
        _parcelId = parcel.Id;

        foreach (var category in KnowledgeGraphSeedData.Categories)
        {
            await _knowledgeGraphService.UpsertLandCategoryAsync(category);
        }

        foreach (var landUse in KnowledgeGraphSeedData.LandUses)
        {
            await _knowledgeGraphService.UpsertLandUseAsync(landUse);
        }

        await _knowledgeGraphService.UpsertAdministrativeAreaAsync(
            KnowledgeGraphSeedData.SyntheticWesternColomboArea);
        await _knowledgeGraphService.UpsertRegulationAsync(
            KnowledgeGraphSeedData.SyntheticStateLandRegulation);
        await _knowledgeGraphService.UpsertSpatialConstraintAsync(
            KnowledgeGraphSeedData.SyntheticBufferZoneConstraint);
        await _knowledgeGraphService.UpsertInfrastructureFeatureAsync(
            KnowledgeGraphSeedData.SyntheticHighwayFeature);
        await _knowledgeGraphService.UpsertEnvironmentalAreaAsync(
            KnowledgeGraphSeedData.SyntheticWetlandArea);

        await _knowledgeGraphService.SyncLandParcelGraphAsync(
            parcel,
            KnowledgeGraphSeedData.GetCategoryId(LandCategoryType.StateLand),
            KnowledgeGraphSeedData.GetLandUseId(LandUseType.Agricultural),
            KnowledgeGraphSeedData.SyntheticWesternColomboAreaId);

        await _knowledgeGraphService.LinkParcelToRegulationAsync(
            parcel.Id,
            KnowledgeGraphSeedData.SyntheticStateLandRegulation.Id);
        await _knowledgeGraphService.LinkParcelToSpatialConstraintAsync(
            parcel.Id,
            KnowledgeGraphSeedData.SyntheticBufferZoneConstraint.Id);
        await _knowledgeGraphService.LinkParcelToInfrastructureFeatureAsync(
            parcel.Id,
            KnowledgeGraphSeedData.SyntheticHighwayFeature.Id,
            850m);
        await _knowledgeGraphService.LinkParcelToEnvironmentalAreaAsync(
            parcel.Id,
            KnowledgeGraphSeedData.SyntheticWetlandArea.Id);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);

        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.LocatedIn);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.HasCategory);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.HasUse);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.SubjectTo);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.HasRestriction);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.Near);
        Assert.Contains(relationships, r => r.RelationshipType == GraphRelationshipTypes.RelatedTo);

        var parcelIds = await _knowledgeGraphService.GetParcelIdsByCategoryAsync(
            KnowledgeGraphSeedData.GetCategoryId(LandCategoryType.StateLand));

        Assert.Contains(parcel.Id, parcelIds);
    }

    [Fact]
    public async Task Sync_replaces_stale_has_use_relationship_after_update()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_knowledgeGraphService);

        var parcel = CreateSyntheticParcel();
        _parcelId = parcel.Id;

        await _knowledgeGraphService.SyncLandParcelGraphAsync(
            parcel,
            KnowledgeGraphSeedData.GetCategoryId(LandCategoryType.StateLand),
            KnowledgeGraphSeedData.GetLandUseId(LandUseType.Agricultural),
            KnowledgeGraphSeedData.SyntheticWesternColomboAreaId);

        parcel.UpdateCurrentUse(new LandUse(LandUseType.Commercial, "[SYNTHETIC] Updated commercial use"));

        await _knowledgeGraphService.SyncLandParcelGraphAsync(parcel);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        var hasUseRelationships = relationships
            .Where(r => r.RelationshipType == GraphRelationshipTypes.HasUse)
            .ToList();

        Assert.Single(hasUseRelationships);
        Assert.Equal(
            KnowledgeGraphSeedData.GetLandUseId(LandUseType.Commercial).ToString(),
            hasUseRelationships[0].TargetNodeId);
    }

    [Fact]
    public async Task Sync_twice_does_not_duplicate_has_category_relationship()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_knowledgeGraphService);

        var parcel = CreateSyntheticParcel();
        _parcelId = parcel.Id;

        await _knowledgeGraphService.SyncLandParcelGraphAsync(parcel);
        await _knowledgeGraphService.SyncLandParcelGraphAsync(parcel);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        var hasCategoryRelationships = relationships
            .Where(r => r.RelationshipType == GraphRelationshipTypes.HasCategory)
            .ToList();

        Assert.Single(hasCategoryRelationships);
    }

    [Fact]
    public async Task DeleteLandParcelGraphAsync_removes_parcel_but_preserves_shared_reference_nodes()
    {
        EnvFileLoader.LoadFromRepositoryRoot(AppContext.BaseDirectory);

        if (!Neo4jSettings.IsConfigured())
        {
            return;
        }

        Assert.NotNull(_knowledgeGraphService);

        var parcel = CreateSyntheticParcel();
        _parcelId = parcel.Id;
        var categoryId = KnowledgeGraphSeedData.GetCategoryId(LandCategoryType.StateLand);

        await _knowledgeGraphService.UpsertLandCategoryAsync(
            KnowledgeGraphSeedData.Categories.First(category => category.Id == categoryId));
        await _knowledgeGraphService.UpsertAdministrativeAreaAsync(
            KnowledgeGraphSeedData.SyntheticWesternColomboArea);

        await _knowledgeGraphService.SyncLandParcelGraphAsync(
            parcel,
            categoryId,
            KnowledgeGraphSeedData.GetLandUseId(LandUseType.Agricultural),
            KnowledgeGraphSeedData.SyntheticWesternColomboAreaId);

        var relationshipsBeforeDelete = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.NotEmpty(relationshipsBeforeDelete);

        await _knowledgeGraphService.DeleteLandParcelGraphAsync(parcel.Id);

        var relationshipsAfterDelete = await _knowledgeGraphService.GetRelationshipsAsync(parcel.Id);
        Assert.Empty(relationshipsAfterDelete);

        var remainingParcelIds = await _knowledgeGraphService.GetParcelIdsByCategoryAsync(categoryId);
        Assert.DoesNotContain(parcel.Id, remainingParcelIds);

        _parcelId = Guid.Empty;
    }

    public async Task DisposeAsync()
    {
        if (_knowledgeGraphService is not null && _parcelId != Guid.Empty)
        {
            await _knowledgeGraphService.DeleteLandParcelGraphAsync(_parcelId);
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    private static LandParcel CreateSyntheticParcel()
    {
        var parcel = new LandParcel(
            new ParcelIdentifier($"SYNTHETIC-NEO4J-{Guid.NewGuid():N}"[..28], "SYNTHETIC-PLAN-001"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Neo4j integration parcel"),
            new LandArea(3.2m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS", "GN-Neo4j-Test"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC] Test use"));

        parcel.AddSpatialConstraint(new SpatialConstraint(
            SpatialConstraintType.BufferZone,
            "[SYNTHETIC] Parcel-owned buffer constraint",
            RestrictionSeverity.Medium));

        parcel.AddRegulatoryReference(new RegulatoryReference(
            "SYNTH-GZ-PARCEL-001",
            "[SYNTHETIC] Parcel regulation",
            new DateOnly(2026, 2, 1)));

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Road,
            "[SYNTHETIC] Access road",
            120m));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            "[SYNTHETIC] Parcel wetland adjacency",
            RestrictionSeverity.High));

        return parcel;
    }
}
