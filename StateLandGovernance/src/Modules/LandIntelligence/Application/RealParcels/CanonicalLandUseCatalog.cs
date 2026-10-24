using System.Text;

namespace StateLandGovernance.LandIntelligence.Application.RealParcels;

/// <summary>
/// One user-facing land-use option. A parcel matches when its original source-reported cadastral
/// label/code is mapped here, when its project GIS supplement group is mapped here, or both.
/// The mapping never rewrites stored cadastral land use or the GIS supplement classification.
/// </summary>
public sealed record CanonicalLandUseDefinition(
    string Key,
    string Label,
    IReadOnlyList<string> SourceLandUseCodes,
    IReadOnlyList<string> SourceLandUseLabels,
    IReadOnlyList<string> GisSupplementGroups);

/// <summary>Per-parcel evidence rows. A parcel may appear several times; counts are always by distinct parcel id.</summary>
public sealed record CanonicalLandUseParcelEvidence(
    Guid ParcelId,
    string? SourceLandUseCode,
    string? SourceLandUseLabel,
    string? GisSupplementGroup);

/// <summary>
/// Distinct-parcel counts for one canonical option.
/// <see cref="ParcelCount"/> is the union; the other fields describe where the matches came from.
/// </summary>
public sealed record CanonicalLandUseOptionCount(
    string Key,
    string Label,
    int ParcelCount,
    int SourceMatchedParcelCount,
    int GisMatchedParcelCount,
    int BothSourcesParcelCount);

/// <summary>
/// Canonical, de-duplicated land-use filter list for Find Suitable Land.
/// Order is the order shown to users.
/// </summary>
public static class CanonicalLandUseCatalog
{
    public static IReadOnlyList<CanonicalLandUseDefinition> Definitions { get; } =
    [
        new("coconut", "Coconut", ["75"], ["Coconut"], ["Coconut"]),
        new("wetland", "Wetland", ["8"], ["Marshy Land"], ["Wetland"]),
        new("scrub-low-jungle", "Scrub / Low Jungle", ["43"], ["Low Jungle"], ["Scrub / low jungle"]),
        new("paddy-field", "Paddy Field", ["73"], ["Paddy Field"], []),
        new("homegarden", "Homegarden", ["1"], ["Homegarden"], []),
        new("garden", "Garden", ["2"], ["Garden"], []),
        new("open-waste-land", "Open Waste Land", ["6"], ["Open Waste Land"], []),
        new("barren-land", "Barren Land", ["9"], ["Barren Land"], []),
        new("rubber", "Rubber", ["76"], ["Rubber"], []),
        new("other", "Other", ["13"], ["Other"], ["Other State parcel"]),
    ];

    /// <summary>
    /// Resolves a canonical key, canonical label, or a legacy GIS group name
    /// (for example "Scrub / low jungle" or "Other State parcel") to a definition.
    /// </summary>
    public static CanonicalLandUseDefinition? TryResolve(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var definition in Definitions)
        {
            if (Normalize(definition.Key) == normalized
                || Normalize(definition.Label) == normalized
                || definition.GisSupplementGroups.Any(group => Normalize(group) == normalized))
            {
                return definition;
            }
        }

        return null;
    }

    public static CanonicalLandUseDefinition? TryMapSource(string? code, string? label)
    {
        var normalizedCode = code?.Trim();
        var normalizedLabel = Normalize(label);
        foreach (var definition in Definitions)
        {
            if (!string.IsNullOrEmpty(normalizedCode)
                && definition.SourceLandUseCodes.Contains(normalizedCode, StringComparer.Ordinal))
            {
                return definition;
            }
        }

        if (normalizedLabel.Length == 0)
        {
            return null;
        }

        return Definitions.FirstOrDefault(definition =>
            definition.SourceLandUseLabels.Any(sourceLabel => Normalize(sourceLabel) == normalizedLabel));
    }

    public static CanonicalLandUseDefinition? TryMapGisGroup(string? group)
    {
        var normalized = Normalize(group);
        if (normalized.Length == 0)
        {
            return null;
        }

        return Definitions.FirstOrDefault(definition =>
            definition.GisSupplementGroups.Any(candidate => Normalize(candidate) == normalized));
    }

    /// <summary>
    /// Counts distinct parcel ids per canonical option. A parcel whose original label and GIS
    /// classification both map to the same option is counted once. Every canonical option is returned,
    /// in display order, including options with a zero count.
    /// </summary>
    public static IReadOnlyList<CanonicalLandUseOptionCount> CountDistinctParcels(
        IEnumerable<CanonicalLandUseParcelEvidence> evidence)
    {
        var sourceIds = Definitions.ToDictionary(d => d.Key, _ => new HashSet<Guid>());
        var gisIds = Definitions.ToDictionary(d => d.Key, _ => new HashSet<Guid>());

        foreach (var row in evidence)
        {
            var source = TryMapSource(row.SourceLandUseCode, row.SourceLandUseLabel);
            if (source is not null)
            {
                sourceIds[source.Key].Add(row.ParcelId);
            }

            var gis = TryMapGisGroup(row.GisSupplementGroup);
            if (gis is not null)
            {
                gisIds[gis.Key].Add(row.ParcelId);
            }
        }

        return Definitions
            .Select(definition =>
            {
                var source = sourceIds[definition.Key];
                var gis = gisIds[definition.Key];
                var both = source.Count(gis.Contains);
                return new CanonicalLandUseOptionCount(
                    definition.Key,
                    definition.Label,
                    source.Count + gis.Count - both,
                    source.Count,
                    gis.Count,
                    both);
            })
            .ToList();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
