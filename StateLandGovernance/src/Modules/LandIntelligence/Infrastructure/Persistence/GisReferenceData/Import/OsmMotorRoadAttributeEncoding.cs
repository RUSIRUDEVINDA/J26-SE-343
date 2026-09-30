using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

/// <summary>
/// Encodes OSM highway class into <c>gis_roads.Name</c> without a schema migration.
/// Format: <c>[{highway}]</c> or <c>[{highway}] {displayName}</c> (max 200 chars).
/// </summary>
internal static class OsmMotorRoadAttributeEncoding
{
    public const int NameMaxLength = 200;

    public static string EncodeName(string highway, string? displayName)
    {
        var hw = highway.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Truncate($"[{hw}]");
        }

        return Truncate($"[{hw}] {displayName.Trim()}");
    }

    public static string? TryDecodeHighway(string? encodedName)
    {
        if (string.IsNullOrWhiteSpace(encodedName) || encodedName[0] != '[')
        {
            return null;
        }

        var close = encodedName.IndexOf(']');
        if (close <= 1)
        {
            return null;
        }

        return encodedName[1..close].Trim();
    }

    public static string? TryDecodeDisplayName(string? encodedName)
    {
        if (string.IsNullOrWhiteSpace(encodedName) || encodedName[0] != '[')
        {
            return encodedName;
        }

        var close = encodedName.IndexOf(']');
        if (close < 0 || close >= encodedName.Length - 1)
        {
            return null;
        }

        var display = encodedName[(close + 1)..].Trim();
        return string.IsNullOrWhiteSpace(display) ? null : display;
    }

    public static GisRoadType MapHighwayToRoadType(string highway) =>
        highway.Trim().ToLowerInvariant() switch
        {
            "motorway" or "motorway_link" => GisRoadType.Expressway,
            "trunk" or "trunk_link" or "primary" or "primary_link" => GisRoadType.Primary,
            "secondary" or "secondary_link" => GisRoadType.Secondary,
            "tertiary" or "tertiary_link" => GisRoadType.Tertiary,
            _ => GisRoadType.Unspecified
        };

    public static string BuildProvenanceSourceName(string? filterPolicyVersion) =>
        string.IsNullOrWhiteSpace(filterPolicyVersion)
            ? GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName
            : $"{GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName}/{GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer}@{filterPolicyVersion}";

    private static string Truncate(string value)
    {
        if (value.Length <= NameMaxLength)
        {
            return value;
        }

        return value[..NameMaxLength];
    }

    public static string? ReadDisplayNameFromAttributes(
        Func<string, string?> readAttribute)
    {
        foreach (var key in new[] { "name", "name_en", "name_latin", "name_si", "name_ta" })
        {
            var value = readAttribute(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static string FormatSnapshotDate(string? snapshot) =>
        string.IsNullOrWhiteSpace(snapshot) ? "unknown" : snapshot.Trim();
}
