using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LandIntelligence.Provenance;

public sealed class AttributeProvenanceTests
{
    [Fact]
    public void Official_factory_sets_verified_official_source()
    {
        var collectedAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var provenance = AttributeProvenance.Official("Survey Department", verified: true, collectedAt);

        Assert.Equal(AttributeProvenanceSourceType.Official, provenance.SourceType);
        Assert.Equal("Survey Department", provenance.SourceName);
        Assert.True(provenance.Verified);
        Assert.Equal(1m, provenance.Confidence);
        Assert.Equal(collectedAt, provenance.CollectedAt);
    }

    [Fact]
    public void Derived_factory_preserves_confidence_and_source_name()
    {
        var provenance = AttributeProvenance.Derived("PostGIS buffer analysis", confidence: 0.92m);

        Assert.Equal(AttributeProvenanceSourceType.Derived, provenance.SourceType);
        Assert.Equal("PostGIS buffer analysis", provenance.SourceName);
        Assert.Equal(0.92m, provenance.Confidence);
        Assert.False(provenance.Verified);
    }

    [Fact]
    public void Synthetic_factory_marks_unverified_synthetic_source()
    {
        var provenance = AttributeProvenance.Synthetic("Synthetic GIS dataset");

        Assert.Equal(AttributeProvenanceSourceType.Synthetic, provenance.SourceType);
        Assert.Equal("Synthetic GIS dataset", provenance.SourceName);
        Assert.False(provenance.Verified);
    }

    [Fact]
    public void Unknown_factory_is_default_when_provenance_not_recorded()
    {
        var provenance = AttributeProvenance.Unknown("characteristics.soilType");

        Assert.Equal(AttributeProvenanceSourceType.Unknown, provenance.SourceType);
        Assert.Equal("characteristics.soilType", provenance.SourceName);
        Assert.Null(provenance.Confidence);
        Assert.False(provenance.Verified);
    }

    [Fact]
    public void Constructor_rejects_confidence_outside_zero_to_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AttributeProvenance(AttributeProvenanceSourceType.Derived, "test", confidence: 1.5m));
    }
}
