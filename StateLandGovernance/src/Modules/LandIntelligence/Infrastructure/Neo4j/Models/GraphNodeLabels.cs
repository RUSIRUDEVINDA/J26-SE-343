namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

/// <summary>
/// Neo4j node labels for Component 1 knowledge graph.
/// </summary>
public static class GraphNodeLabels
{
    public const string LandParcel = "LandParcel";
    public const string AdministrativeArea = "AdministrativeArea";
    public const string LandCategory = "LandCategory";
    public const string LandUse = "LandUse";
    public const string SpatialConstraint = "SpatialConstraint";
    public const string Regulation = "Regulation";
    public const string InfrastructureFeature = "InfrastructureFeature";
    public const string EnvironmentalArea = "EnvironmentalArea";
}
