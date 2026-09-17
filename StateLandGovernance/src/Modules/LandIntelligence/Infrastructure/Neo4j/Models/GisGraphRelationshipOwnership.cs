namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

/// <summary>
/// Relationship property marker for records owned by the GIS enrichment persistence pipeline.
/// </summary>
public static class GisGraphRelationshipOwnership
{
    public const string SourceProperty = "source";

    public const string DerivedAtProperty = "derivedAt";

    public const string SourceName = "LandIntelligence_GIS_DerivedEnrichment";
}
