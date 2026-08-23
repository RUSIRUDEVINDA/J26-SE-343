using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.Persistence;

public sealed class GisReferenceDataPersistenceConfigurationTests
{
    [Fact]
    public void Gis_reference_entities_map_to_land_intelligence_schema_tables()
    {
        var model = CreateModel();

        AssertTable(model, typeof(GisAdministrativeBoundaryEntity), "gis_administrative_boundaries");
        AssertTable(model, typeof(GisRoadEntity), "gis_roads");
        AssertTable(model, typeof(GisWaterFeatureEntity), "gis_water_features");
        AssertTable(model, typeof(GisSoilGroupEntity), "gis_soil_groups");
        AssertTable(model, typeof(GisSoilConservationAreaEntity), "gis_soil_conservation_areas");
        AssertTable(model, typeof(GisSoilErosionObservationEntity), "gis_soil_erosion_observations");
    }

    [Fact]
    public void Gis_reference_entities_configure_provenance_fields()
    {
        var model = CreateModel();

        foreach (var entityType in GetGisReferenceEntityTypes())
        {
            AssertProperty(model, entityType, "SourceName", maxLength: 200, isRequired: true);
            AssertProperty(model, entityType, "SourceLayer", maxLength: 200, isRequired: true);
            AssertProperty(
                model,
                entityType,
                "SourceFeatureId",
                maxLength: GisReferenceEntityBase.SourceFeatureIdMaxLength,
                isRequired: false);
            AssertProperty(
                model,
                entityType,
                "SourceFingerprint",
                maxLength: GisReferenceEntityBase.SourceFingerprintMaxLength,
                isRequired: false);
            AssertProperty(
                model,
                entityType,
                "ImportedAt",
                storeType: "timestamp with time zone",
                isRequired: true);
        }
    }

    [Fact]
    public void Gis_reference_entities_require_source_feature_id_or_fingerprint()
    {
        var model = CreateModel();

        foreach (var entityType in GetGisReferenceEntityTypes())
        {
            var entity = model.FindEntityType(entityType)!;
            var tableName = entity.GetTableName()!;

            Assert.Contains(
                entity.GetCheckConstraints(),
                constraint => constraint.Name == $"CK_{tableName}_source_identity");
        }
    }

    [Fact]
    public void Gis_reference_entities_prevent_duplicate_source_feature_ids_within_source_and_layer()
    {
        var model = CreateModel();

        foreach (var entityType in GetGisReferenceEntityTypes())
        {
            AssertUniqueIndex(
                model,
                entityType,
                ["SourceName", "SourceLayer", "SourceFeatureId"],
                filterContains: "SourceFeatureId\" IS NOT NULL");
        }
    }

    [Fact]
    public void Gis_reference_entities_allow_same_source_feature_id_across_different_layers()
    {
        var model = CreateModel();

        foreach (var entityType in GetGisReferenceEntityTypes())
        {
            var index = FindUniqueIndex(model, entityType, "SourceFeatureId");

            Assert.Contains(index.Properties, property => property.Name == "SourceLayer");
        }
    }

    [Fact]
    public void Gis_reference_entities_prevent_duplicate_fingerprints_when_source_feature_id_is_absent()
    {
        var model = CreateModel();

        foreach (var entityType in GetGisReferenceEntityTypes())
        {
            AssertUniqueIndex(
                model,
                entityType,
                ["SourceName", "SourceLayer", "SourceFingerprint"],
                filterContains: "SourceFeatureId\" IS NULL");
        }
    }

    [Theory]
    [InlineData(typeof(GisAdministrativeBoundaryEntity), "Boundary", "geometry (MultiPolygon, 4326)")]
    [InlineData(typeof(GisRoadEntity), "Geometry", "geometry (MultiLineString, 4326)")]
    [InlineData(typeof(GisWaterFeatureEntity), "Geometry", "geometry (Geometry, 4326)")]
    [InlineData(typeof(GisSoilGroupEntity), "Boundary", "geometry (MultiPolygon, 4326)")]
    [InlineData(typeof(GisSoilConservationAreaEntity), "Boundary", "geometry (MultiPolygon, 4326)")]
    [InlineData(typeof(GisSoilErosionObservationEntity), "Location", "geometry (Point, 4326)")]
    public void Gis_reference_entities_configure_postgis_geometry_columns(
        Type entityType,
        string geometryPropertyName,
        string expectedStoreType)
    {
        var model = CreateModel();

        AssertProperty(
            model,
            entityType,
            geometryPropertyName,
            storeType: expectedStoreType,
            isRequired: true);
    }

    [Fact]
    public void Gis_reference_geometry_columns_have_spatial_indexes()
    {
        var model = CreateModel();

        AssertSpatialIndex(model, typeof(GisAdministrativeBoundaryEntity), "Boundary");
        AssertSpatialIndex(model, typeof(GisRoadEntity), "Geometry");
        AssertSpatialIndex(model, typeof(GisWaterFeatureEntity), "Geometry");
        AssertSpatialIndex(model, typeof(GisSoilGroupEntity), "Boundary");
        AssertSpatialIndex(model, typeof(GisSoilConservationAreaEntity), "Boundary");
        AssertSpatialIndex(model, typeof(GisSoilErosionObservationEntity), "Location");
    }

    [Fact]
    public void DbContext_exposes_gis_reference_db_sets_without_affecting_parcel_sets()
    {
        using var context = CreateContext();

        Assert.NotNull(context.GisAdministrativeBoundaries);
        Assert.NotNull(context.GisRoads);
        Assert.NotNull(context.GisWaterFeatures);
        Assert.NotNull(context.GisSoilGroups);
        Assert.NotNull(context.GisSoilConservationAreas);
        Assert.NotNull(context.GisSoilErosionObservations);
        Assert.NotNull(context.LandParcels);
        Assert.NotNull(context.SpatialConstraints);
        Assert.NotNull(context.InfrastructureFeatures);
        Assert.NotNull(context.EnvironmentalRestrictions);
    }

    private static IModel CreateModel()
    {
        using var context = CreateContext();
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static LandIntelligenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=gis_reference_model_test;Username=test;Password=test",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        return new LandIntelligenceDbContext(options);
    }

    private static IEnumerable<Type> GetGisReferenceEntityTypes() =>
    [
        typeof(GisAdministrativeBoundaryEntity),
        typeof(GisRoadEntity),
        typeof(GisWaterFeatureEntity),
        typeof(GisSoilGroupEntity),
        typeof(GisSoilConservationAreaEntity),
        typeof(GisSoilErosionObservationEntity)
    ];

    private static void AssertTable(IModel model, Type entityType, string tableName)
    {
        var entity = model.FindEntityType(entityType)!;

        Assert.Equal(LandIntelligenceDbContext.SchemaName, entity.GetSchema());
        Assert.Equal(tableName, entity.GetTableName());
    }

    private static void AssertProperty(
        IModel model,
        Type entityType,
        string propertyName,
        int? maxLength = null,
        string? storeType = null,
        bool? isRequired = null)
    {
        var property = model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        if (maxLength.HasValue)
        {
            Assert.Equal(maxLength.Value, property.GetMaxLength());
        }

        if (storeType is not null)
        {
            Assert.Equal(storeType, property.GetColumnType());
        }

        if (isRequired.HasValue)
        {
            Assert.Equal(isRequired.Value, !property.IsNullable);
        }
    }

    private static void AssertSpatialIndex(IModel model, Type entityType, string propertyName)
    {
        var entity = model.FindEntityType(entityType)!;
        var property = entity.FindProperty(propertyName)!;

        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0] == property);
    }

    private static void AssertUniqueIndex(
        IModel model,
        Type entityType,
        IReadOnlyList<string> propertyNames,
        string filterContains)
    {
        var index = FindUniqueIndex(model, entityType, propertyNames[^1]);

        Assert.Equal(propertyNames.Count, index.Properties.Count);

        for (var i = 0; i < propertyNames.Count; i++)
        {
            Assert.Equal(propertyNames[i], index.Properties[i].Name);
        }

        Assert.True(index.IsUnique);
        Assert.NotNull(index.GetFilter());
        Assert.Contains(filterContains, index.GetFilter(), StringComparison.Ordinal);
    }

    private static IIndex FindUniqueIndex(IModel model, Type entityType, string terminalPropertyName)
    {
        var entity = model.FindEntityType(entityType)!;

        return entity.GetIndexes().Single(index =>
            index.IsUnique &&
            index.Properties[^1].Name == terminalPropertyName);
    }
}
