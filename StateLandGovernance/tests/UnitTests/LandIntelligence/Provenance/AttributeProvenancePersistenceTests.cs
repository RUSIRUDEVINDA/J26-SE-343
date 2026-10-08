using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.UnitTests.LandIntelligence.Provenance;

public sealed class AttributeProvenancePersistenceTests
{
    [Theory]
    [InlineData(AttributeProvenanceSourceType.Official)]
    [InlineData(AttributeProvenanceSourceType.Derived)]
    [InlineData(AttributeProvenanceSourceType.Synthetic)]
    [InlineData(AttributeProvenanceSourceType.Unknown)]
    public void Serialize_deserialize_round_trips_source_type(AttributeProvenanceSourceType sourceType)
    {
        var original = new AttributeProvenance(
            sourceType,
            "Test source",
            confidence: 0.8m,
            collectedAt: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            verified: sourceType == AttributeProvenanceSourceType.Official);

        var json = AttributeProvenancePersistenceMapper.Serialize(original);
        var restored = AttributeProvenancePersistenceMapper.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(original.SourceType, restored!.SourceType);
        Assert.Equal(original.SourceName, restored.SourceName);
        Assert.Equal(original.Confidence, restored.Confidence);
        Assert.Equal(original.CollectedAt, restored.CollectedAt);
        Assert.Equal(original.Verified, restored.Verified);
    }

    [Fact]
    public void SerializeCharacteristicsProvenance_round_trips_per_attribute_provenance()
    {
        var characteristics = new LandCharacteristics(
            soilType: "Loam",
            terrainDescription: "Flat",
            elevationMeters: 120m,
            soilTypeProvenance: AttributeProvenance.Official("Survey Department"),
            terrainDescriptionProvenance: AttributeProvenance.Derived("DEM slope model"),
            elevationMetersProvenance: AttributeProvenance.Synthetic("Synthetic GIS dataset"));

        var json = AttributeProvenancePersistenceMapper.SerializeCharacteristicsProvenance(characteristics);
        var (soil, terrain, elevation) =
            AttributeProvenancePersistenceMapper.DeserializeCharacteristicsProvenance(json);

        Assert.Equal(AttributeProvenanceSourceType.Official, soil!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, terrain!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Synthetic, elevation!.SourceType);
    }

    [Fact]
    public void Serialize_returns_null_when_provenance_is_null()
    {
        Assert.Null(AttributeProvenancePersistenceMapper.Serialize(null));
    }
}
