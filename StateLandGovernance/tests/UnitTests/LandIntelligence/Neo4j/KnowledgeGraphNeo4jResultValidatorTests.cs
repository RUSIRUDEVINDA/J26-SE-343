using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class KnowledgeGraphNeo4jResultValidatorTests
{
    [Fact]
    public void HasGisIntelligenceConflict_detects_located_in_province_mismatch()
    {
        var baseline = CreateIntelligence(province: "Southern", district: "Hambantota");
        var neo4j = CreateIntelligence(province: "Western", district: "Hambantota");

        var conflict = KnowledgeGraphNeo4jResultValidator.HasGisIntelligenceConflict(
            baseline,
            neo4j,
            out var reason);

        Assert.True(conflict);
        Assert.Contains("province mismatch", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HasRelationshipConflict_detects_located_in_district_mismatch()
    {
        var baselineDistrictId = Guid.NewGuid();
        var neo4jDistrictId = Guid.NewGuid();

        var baseline = new[]
        {
            CreateRelationship(GraphRelationshipTypes.LocatedIn, "District", baselineDistrictId)
        };
        var neo4j = new[]
        {
            CreateRelationship(GraphRelationshipTypes.LocatedIn, "District", neo4jDistrictId)
        };

        var conflict = KnowledgeGraphNeo4jResultValidator.HasRelationshipConflict(
            baseline,
            neo4j,
            out var reason);

        Assert.True(conflict);
        Assert.Contains("LOCATED_IN", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void HasGisIntelligenceConflict_returns_false_when_baseline_matches_neo4j()
    {
        var provinceId = Guid.NewGuid();
        var districtId = Guid.NewGuid();
        var baseline = CreateIntelligence(
            province: "Southern",
            district: "Hambantota",
            provinceId: provinceId,
            districtId: districtId);
        var neo4j = CreateIntelligence(
            province: "Southern",
            district: "Hambantota",
            provinceId: provinceId,
            districtId: districtId);

        var conflict = KnowledgeGraphNeo4jResultValidator.HasGisIntelligenceConflict(
            baseline,
            neo4j,
            out _);

        Assert.False(conflict);
    }

    private static LandParcelGisGraphIntelligenceDto CreateIntelligence(
        string province,
        string district,
        Guid? provinceId = null,
        Guid? districtId = null) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            DetectedProvince = province,
            ProvinceReferenceId = provinceId ?? Guid.NewGuid(),
            DetectedDistrict = district,
            DistrictReferenceId = districtId ?? Guid.NewGuid(),
            ConservationAreas = []
        };

    private static LandRelationshipDto CreateRelationship(
        string relationshipType,
        string targetNodeType,
        Guid targetNodeId) =>
        new(
            relationshipType,
            "LandParcel",
            Guid.NewGuid().ToString(),
            targetNodeType,
            targetNodeId.ToString(),
            null);
}
