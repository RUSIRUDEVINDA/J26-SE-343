using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisReferenceData;

public sealed class GisReferenceImportIdentityTests
{
    [Fact]
    public void ResolveSourceFeatureId_uses_objectid_attribute_when_present()
    {
        var feature = CreateFeature(new AttributesTable { { "objectid", 844619 } }, null);

        var sourceFeatureId = GisReferenceImportIdentityResolver.ResolveSourceFeatureId(feature);

        Assert.Equal("844619", sourceFeatureId);
    }

    [Fact]
    public void Resolve_uses_source_feature_id_when_objectid_exists()
    {
        var feature = CreateFeature(new AttributesTable { { "objectid", 8 } }, null);

        var (sourceFeatureId, sourceFingerprint) = GisReferenceImportIdentityResolver.Resolve(
            feature,
            "LandIntelligence_GIS",
            "district_boundaries",
            "seed");

        Assert.Equal("8", sourceFeatureId);
        Assert.Null(sourceFingerprint);
    }

    [Fact]
    public void Resolve_uses_deterministic_fingerprint_when_objectid_missing()
    {
        var geometry = NtsGeometryServices.Instance
            .CreateGeometryFactory(4326)
            .CreatePoint(new Coordinate(80.1, 6.1));
        var feature = CreateFeature(new AttributesTable { { "name", "test" } }, geometry);

        var first = GisReferenceImportIdentityResolver.Resolve(
            feature,
            "LandIntelligence_GIS",
            "soil_groups",
            "seed-value");
        var second = GisReferenceImportIdentityResolver.Resolve(
            feature,
            "LandIntelligence_GIS",
            "soil_groups",
            "seed-value");

        Assert.Null(first.SourceFeatureId);
        Assert.NotNull(first.SourceFingerprint);
        Assert.Equal(first.SourceFingerprint, second.SourceFingerprint);
        Assert.Equal(64, first.SourceFingerprint!.Length);
    }

    private static Feature CreateFeature(AttributesTable attributes, Geometry? geometry) =>
        new(geometry ?? Geometry.DefaultFactory.CreatePoint(new Coordinate(0, 0)), attributes);
}
