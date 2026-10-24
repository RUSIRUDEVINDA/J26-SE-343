using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StateLandGovernance.LandIntelligence.Application.Serialization;

/// <summary>
/// Accepts JSON null, numbers, or blank/numeric strings for optional decimals.
/// Empty strings from HTML forms must not fail model binding.
/// </summary>
public sealed class FlexibleNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
            {
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                if (decimal.TryParse(
                        raw,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return value;
                }

                throw new JsonException($"The JSON value '{raw}' could not be converted to a decimal.");
            }
            default:
                throw new JsonException(
                    $"Unexpected token {reader.TokenType} when parsing an optional decimal.");
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

/// <summary>
/// Accepts JSON null, ISO date strings, or blank strings for optional dates.
/// </summary>
public sealed class FlexibleNullableDateOnlyConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                {
                    return value;
                }

                throw new JsonException($"The JSON value '{raw}' could not be converted to a date.");
            }
            default:
                throw new JsonException(
                    $"Unexpected token {reader.TokenType} when parsing an optional date.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
}
