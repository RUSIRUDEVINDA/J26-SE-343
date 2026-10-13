namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Authoritative JSON serialization options and helper for PostgreSQL JSONB columns.
/// </summary>
internal static class PersistenceJsonSerializer
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());
        options.Converters.Add(new StronglyTypedStringJsonConverterFactory());
        options.Converters.Add(new IntakeSourceReferenceConverter());
        options.Converters.Add(new MeasurementUnitConverter());
        options.Converters.Add(new LandExtentConverter());
        options.Converters.Add(new JurisdictionContextConverter());

        return options;
    }

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, Options);
    }
}

internal sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var valueProp = typeToConvert.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null || valueProp.PropertyType != typeof(Guid))
        {
            return false;
        }

        var ctor = typeToConvert.GetConstructor(new[] { typeof(Guid) });
        return ctor != null;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

internal sealed class StronglyTypedIdJsonConverter<T> : JsonConverter<T>
{
    private static readonly PropertyInfo ValueProperty = typeof(T).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly ConstructorInfo Constructor = typeof(T).GetConstructor(new[] { typeof(Guid) })!;

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var guid = reader.GetGuid();
            return (T)Constructor.Invoke(new object[] { guid });
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            Guid guid = Guid.Empty;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();
                    if (string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        guid = reader.GetGuid();
                    }
                }
            }

            return (T)Constructor.Invoke(new object[] { guid });
        }

        throw new JsonException($"Unable to parse {typeof(T).Name} from token {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var guid = (Guid)ValueProperty.GetValue(value)!;
        writer.WriteStringValue(guid.ToString());
    }
}

internal sealed class StronglyTypedStringJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert == typeof(string)) return false;

        var valueProp = typeToConvert.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null || valueProp.PropertyType != typeof(string))
        {
            return false;
        }

        var ctor = typeToConvert.GetConstructor(new[] { typeof(string) });
        return ctor != null;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedStringJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

internal sealed class StronglyTypedStringJsonConverter<T> : JsonConverter<T>
{
    private static readonly PropertyInfo ValueProperty = typeof(T).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly ConstructorInfo Constructor = typeof(T).GetConstructor(new[] { typeof(string) })!;

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return (T)Constructor.Invoke(new object[] { str });
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string str = string.Empty;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();
                    if (string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        str = reader.GetString() ?? string.Empty;
                    }
                }
            }

            return (T)Constructor.Invoke(new object[] { str });
        }

        throw new JsonException($"Unable to parse {typeof(T).Name} from token {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var str = (string)ValueProperty.GetValue(value)!;
        writer.WriteStringValue(str);
    }
}

internal sealed class IntakeSourceReferenceConverter : JsonConverter<IntakeSourceReference>
{
    public override IntakeSourceReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject for IntakeSourceReference, got {reader.TokenType}.");
        }

        string? reference = null;
        string? version = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();
                if (string.Equals(propName, "reference", StringComparison.OrdinalIgnoreCase))
                {
                    reference = reader.GetString();
                }
                else if (string.Equals(propName, "version", StringComparison.OrdinalIgnoreCase))
                {
                    version = reader.GetString();
                }
            }
        }

        return new IntakeSourceReference(reference ?? string.Empty, version ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, IntakeSourceReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("reference", value.Reference);
        writer.WriteString("version", value.Version);
        writer.WriteEndObject();
    }
}

internal sealed class MeasurementUnitConverter : JsonConverter<MeasurementUnit>
{
    public override MeasurementUnit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return string.IsNullOrWhiteSpace(str) ? default : new MeasurementUnit(str);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? name = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();
                    if (string.Equals(propName, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        name = reader.GetString();
                    }
                }
            }
            return string.IsNullOrWhiteSpace(name) ? default : new MeasurementUnit(name);
        }

        throw new JsonException($"Unable to parse MeasurementUnit from {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, MeasurementUnit value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}

internal sealed class LandExtentConverter : JsonConverter<LandExtent>
{
    public override LandExtent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject for LandExtent, got {reader.TokenType}.");
        }

        decimal val = 0m;
        MeasurementUnit unit = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();
                if (string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
                {
                    val = reader.GetDecimal();
                }
                else if (string.Equals(propName, "unit", StringComparison.OrdinalIgnoreCase))
                {
                    unit = JsonSerializer.Deserialize<MeasurementUnit>(ref reader, options);
                }
            }
        }

        return new LandExtent(val, unit);
    }

    public override void Write(Utf8JsonWriter writer, LandExtent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        writer.WritePropertyName("unit");
        JsonSerializer.Serialize(writer, value.Unit, options);
        writer.WriteEndObject();
    }
}

internal sealed class JurisdictionContextConverter : JsonConverter<JurisdictionContext>
{
    public override JurisdictionContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject for JurisdictionContext, got {reader.TokenType}.");
        }

        string? code = null;
        string? region = null;
        string? district = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();
                if (string.Equals(propName, "code", StringComparison.OrdinalIgnoreCase))
                {
                    code = reader.GetString();
                }
                else if (string.Equals(propName, "region", StringComparison.OrdinalIgnoreCase))
                {
                    region = reader.GetString();
                }
                else if (string.Equals(propName, "district", StringComparison.OrdinalIgnoreCase))
                {
                    district = reader.GetString();
                }
            }
        }

        return new JurisdictionContext(code ?? string.Empty, region, district);
    }

    public override void Write(Utf8JsonWriter writer, JurisdictionContext value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("code", value.Code);
        if (value.Region != null) writer.WriteString("region", value.Region);
        if (value.District != null) writer.WriteString("district", value.District);
        writer.WriteEndObject();
    }
}

