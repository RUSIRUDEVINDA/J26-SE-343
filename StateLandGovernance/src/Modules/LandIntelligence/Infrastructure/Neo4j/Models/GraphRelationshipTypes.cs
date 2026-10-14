namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

/// <summary>
/// Neo4j relationship types for Component 1 knowledge graph.
/// </summary>
public static class GraphRelationshipTypes
{
    public const string LocatedIn = "LOCATED_IN";
    public const string HasCategory = "HAS_CATEGORY";
    public const string HasUse = "HAS_USE";
    public const string SubjectTo = "SUBJECT_TO";
    public const string HasRestriction = "HAS_RESTRICTION";
    public const string Near = "NEAR";
    public const string RelatedTo = "RELATED_TO";
    public const string NearRoad = "NEAR_ROAD";
    public const string NearWater = "NEAR_WATER";
    public const string HasDerivedSoil = "HAS_DERIVED_SOIL";
    public const string IntersectsConservationArea = "INTERSECTS_CONSERVATION_AREA";
}
