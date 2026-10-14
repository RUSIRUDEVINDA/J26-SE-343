using System.Text.Json;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

internal static class AttributeProvenancePersistenceMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? SerializeCharacteristicsProvenance(LandCharacteristics? characteristics)
    {
        if (characteristics is null)
        {
            return null;
        }

        if (characteristics.SoilTypeProvenance is null
            && characteristics.TerrainDescriptionProvenance is null
            && characteristics.ElevationMetersProvenance is null)
        {
            return null;
        }

        var payload = new CharacteristicsProvenancePayload
        {
            SoilType = ToPayload(characteristics.SoilTypeProvenance),
            TerrainDescription = ToPayload(characteristics.TerrainDescriptionProvenance),
            ElevationMeters = ToPayload(characteristics.ElevationMetersProvenance)
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static (AttributeProvenance? Soil, AttributeProvenance? Terrain, AttributeProvenance? Elevation)
        DeserializeCharacteristicsProvenance(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, null, null);
        }

        var payload = JsonSerializer.Deserialize<CharacteristicsProvenancePayload>(json, JsonOptions);
        if (payload is null)
        {
            return (null, null, null);
        }

        return (
            ToDomain(payload.SoilType),
            ToDomain(payload.TerrainDescription),
            ToDomain(payload.ElevationMeters));
    }

    public static string? Serialize(AttributeProvenance? provenance) =>
        provenance is null ? null : JsonSerializer.Serialize(ToPayload(provenance), JsonOptions);

    public static AttributeProvenance? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<AttributeProvenancePayload>(json, JsonOptions);
        return ToDomain(payload);
    }

    private static AttributeProvenancePayload? ToPayload(AttributeProvenance? provenance) =>
        provenance is null
            ? null
            : new AttributeProvenancePayload
            {
                SourceType = provenance.SourceType,
                SourceName = provenance.SourceName,
                Confidence = provenance.Confidence,
                CollectedAt = provenance.CollectedAt,
                Verified = provenance.Verified
            };

    private static AttributeProvenance? ToDomain(AttributeProvenancePayload? payload) =>
        payload is null
            ? null
            : new AttributeProvenance(
                payload.SourceType,
                payload.SourceName,
                payload.Confidence,
                payload.CollectedAt,
                payload.Verified);

    private sealed class CharacteristicsProvenancePayload
    {
        public AttributeProvenancePayload? SoilType { get; set; }
        public AttributeProvenancePayload? TerrainDescription { get; set; }
        public AttributeProvenancePayload? ElevationMeters { get; set; }
    }

    private sealed class AttributeProvenancePayload
    {
        public AttributeProvenanceSourceType SourceType { get; set; } = AttributeProvenanceSourceType.Unknown;
        public string? SourceName { get; set; }
        public decimal? Confidence { get; set; }
        public DateTimeOffset? CollectedAt { get; set; }
        public bool Verified { get; set; }
    }
}
