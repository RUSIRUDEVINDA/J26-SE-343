namespace StateLandGovernance.LandIntelligence.Application.Configuration;

/// <summary>
/// Local-development GIS enrichment coverage and road-source selection.
/// Defaults preserve existing Hambantota pilot behavior.
/// </summary>
public sealed class GisEnrichmentCoverageOptions
{
    public const string SectionName = "LandIntelligence:GisEnrichmentCoverage";

    /// <summary>
    /// District names (matching gis_administrative_boundaries Name) that are in coverage.
    /// Default: Hambantota only.
    /// </summary>
    public string[] SupportedDistrictNames { get; set; } = [GisEnrichmentCoverageDefaults.Hambantota];

    /// <summary>
    /// Global/fallback road SourceLayer when no district-specific mapping matches.
    /// Default expressways preserves Hambantota pilot behavior.
    /// Null means all layers (legacy).
    /// </summary>
    public string? RoadSourceLayer { get; set; } = GisEnrichmentCoverageDefaults.ExpresswaysLayer;

    /// <summary>
    /// Per-district road SourceLayer overrides. When Colombo is enabled for OSM,
    /// Hambantota can remain on expressways so enabling Colombo does not change
    /// existing Hambantota nearest-road behavior.
    /// </summary>
    public DistrictRoadSourceLayerOptions[] DistrictRoadSourceLayers { get; set; } = [];

    /// <summary>
    /// Optional filter-policy version expected on OSM motor-road imports (documented provenance).
    /// </summary>
    public string? OsmMotorRoadFilterPolicyVersion { get; set; } =
        GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion;

    public string ResolveRoadSourceLayerForDistrict(string? matchedDistrict)
    {
        if (!string.IsNullOrWhiteSpace(matchedDistrict) && DistrictRoadSourceLayers is { Length: > 0 })
        {
            var match = DistrictRoadSourceLayers.FirstOrDefault(item =>
                string.Equals(item.DistrictName, matchedDistrict, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.RoadSourceLayer));

            if (match is not null)
            {
                return match.RoadSourceLayer.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(RoadSourceLayer) ? string.Empty : RoadSourceLayer.Trim();
    }
}

public sealed class DistrictRoadSourceLayerOptions
{
    public string DistrictName { get; set; } = string.Empty;

    public string RoadSourceLayer { get; set; } = string.Empty;
}

public static class GisEnrichmentCoverageDefaults
{
    public const string Hambantota = "Hambantota";

    public const string Colombo = "Colombo";

    public const string ExpresswaysLayer = "expressways";

    public const string OsmMotorRoadsLayer = "osm_motor_roads";

    public const string OsmMotorRoadFilterPolicyVersion = "2026-03-20-colombo-v1";

    public const string OsmMotorRoadSourceName = "LandIntelligence_GIS";

    /// <summary>
    /// Required database name token for disposable Colombo experimental verification DBs.
    /// </summary>
    public const string IsolatedDatabaseNameToken = "colombo_exp_iso";
}
