using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Mapping;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class KnowledgeGraphMapperTests
{
    [Fact]
    public void ToLandParcelNode_maps_identifier_only()
    {
        var parcel = new LandParcel(
            new ParcelIdentifier("SYNTH-001", "PLAN-001"),
            new LandCategory(LandCategoryType.StateLand),
            new LandArea(1.5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"));

        var node = KnowledgeGraphMapper.ToLandParcelNode(parcel);

        Assert.Equal(parcel.Id, node.Id);
        Assert.Equal("SYNTH-001", node.CadastralNumber);
        Assert.Equal("PLAN-001", node.SurveyPlanReference);
    }

    [Fact]
    public void ToSpatialConstraintNode_maps_constraint_properties()
    {
        var constraint = new SpatialConstraint(
            SpatialConstraintType.BufferZone,
            "[SYNTHETIC] Buffer zone",
            RestrictionSeverity.High);

        var node = KnowledgeGraphMapper.ToSpatialConstraintNode(constraint);

        Assert.Equal(constraint.Id, node.Id);
        Assert.Equal(nameof(SpatialConstraintType.BufferZone), node.ConstraintType);
        Assert.Equal("[SYNTHETIC] Buffer zone", node.Description);
        Assert.Equal(nameof(RestrictionSeverity.High), node.Severity);
    }

    [Fact]
    public void ToLinkParameters_includes_distance_for_infrastructure_links()
    {
        var parcelId = Guid.NewGuid();
        var featureId = Guid.NewGuid();

        var parameters = KnowledgeGraphMapper.ToLinkParameters(parcelId, featureId, 250m);

        Assert.Equal(parcelId.ToString(), parameters.GetType().GetProperty("parcelId")!.GetValue(parameters));
        Assert.Equal(featureId.ToString(), parameters.GetType().GetProperty("targetId")!.GetValue(parameters));
        Assert.Equal(250m, parameters.GetType().GetProperty("distanceMeters")!.GetValue(parameters));
    }
}
