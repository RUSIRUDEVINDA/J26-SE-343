namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

// Only committed database state uses this converter; HTTP input must invoke the authorized operation.
internal sealed class HumanEvidenceFactJsonConverter : JsonConverter<HumanEvidenceFact>
{
    public override HumanEvidenceFact Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var json = JsonDocument.ParseValue(ref reader);
        var result = AggregateRehydrator.Create<HumanEvidenceFact>();
        foreach (var property in typeof(HumanEvidenceFact).GetProperties())
        {
            var name = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            if (!json.RootElement.TryGetProperty(name, out var element))
            {
                // Nullable authority metadata was added after the first isolated fixture.
                // Missing historical metadata remains unknown; never manufacture a grant.
                if (Nullable.GetUnderlyingType(property.PropertyType) != null || property.PropertyType == typeof(string)) continue;
                throw new JsonException("Committed human-evidence state is missing " + name);
            }
            var value = property.PropertyType == typeof(AnalysisFactValue)
                ? new AnalysisFactValue(element.GetProperty("kind").Deserialize<AnalysisFactValueKind>(options),
                    element.GetProperty("canonicalValue").GetString()!)
                : element.Deserialize(property.PropertyType, options);
            AggregateRehydrator.SetPropertyBackingField(result, property.Name, value);
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, HumanEvidenceFact value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var property in typeof(HumanEvidenceFact).GetProperties())
        {
            writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name);
            JsonSerializer.Serialize(writer, property.GetValue(value), property.PropertyType, options);
        }
        writer.WriteEndObject();
    }
}
