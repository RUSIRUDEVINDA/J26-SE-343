using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Cypher;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class KnowledgeGraphModelTests
{
    [Fact]
    public void GraphNodeLabels_define_expected_node_types()
    {
        Assert.Equal("LandParcel", GraphNodeLabels.LandParcel);
        Assert.Equal("AdministrativeArea", GraphNodeLabels.AdministrativeArea);
        Assert.Equal("LandCategory", GraphNodeLabels.LandCategory);
        Assert.Equal("LandUse", GraphNodeLabels.LandUse);
        Assert.Equal("SpatialConstraint", GraphNodeLabels.SpatialConstraint);
        Assert.Equal("Regulation", GraphNodeLabels.Regulation);
        Assert.Equal("InfrastructureFeature", GraphNodeLabels.InfrastructureFeature);
        Assert.Equal("EnvironmentalArea", GraphNodeLabels.EnvironmentalArea);
        Assert.Equal("Province", GraphNodeLabels.Province);
        Assert.Equal("District", GraphNodeLabels.District);
        Assert.Equal("Road", GraphNodeLabels.Road);
        Assert.Equal("WaterFeature", GraphNodeLabels.WaterFeature);
        Assert.Equal("SoilGroup", GraphNodeLabels.SoilGroup);
        Assert.Equal("ConservationArea", GraphNodeLabels.ConservationArea);
    }

    [Fact]
    public void GraphRelationshipTypes_define_expected_relationships()
    {
        Assert.Equal("LOCATED_IN", GraphRelationshipTypes.LocatedIn);
        Assert.Equal("HAS_CATEGORY", GraphRelationshipTypes.HasCategory);
        Assert.Equal("HAS_USE", GraphRelationshipTypes.HasUse);
        Assert.Equal("SUBJECT_TO", GraphRelationshipTypes.SubjectTo);
        Assert.Equal("HAS_RESTRICTION", GraphRelationshipTypes.HasRestriction);
        Assert.Equal("NEAR", GraphRelationshipTypes.Near);
        Assert.Equal("RELATED_TO", GraphRelationshipTypes.RelatedTo);
        Assert.Equal("NEAR_ROAD", GraphRelationshipTypes.NearRoad);
        Assert.Equal("NEAR_WATER", GraphRelationshipTypes.NearWater);
        Assert.Equal("HAS_DERIVED_SOIL", GraphRelationshipTypes.HasDerivedSoil);
        Assert.Equal("INTERSECTS_CONSERVATION_AREA", GraphRelationshipTypes.IntersectsConservationArea);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void BuildTraverseFromParcelQuery_injects_validated_depth(int depth)
    {
        var query = KnowledgeGraphCypher.BuildTraverseFromParcelQuery(depth);

        Assert.Contains($"[*1..{depth}]", query);
        Assert.Contains("relatedParcelId", query);
    }

    [Fact]
    public void DeleteLandParcelGraph_deletes_only_land_parcel_node()
    {
        Assert.Contains("MATCH (p:LandParcel { id: $parcelId })", KnowledgeGraphCypher.DeleteLandParcelGraph);
        Assert.Contains("DETACH DELETE p", KnowledgeGraphCypher.DeleteLandParcelGraph);
        Assert.DoesNotContain("LandCategory", KnowledgeGraphCypher.DeleteLandParcelGraph);
        Assert.DoesNotContain("AdministrativeArea", KnowledgeGraphCypher.DeleteLandParcelGraph);
    }

    [Fact]
    public void ClearParcelRelationships_removes_outgoing_relationships_only()
    {
        Assert.Contains("MATCH (p:LandParcel { id: $parcelId })-[r]->()", KnowledgeGraphCypher.ClearParcelRelationships);
        Assert.Contains("DELETE r", KnowledgeGraphCypher.ClearParcelRelationships);
        Assert.DoesNotContain("DETACH DELETE p", KnowledgeGraphCypher.ClearParcelRelationships);
    }
}
