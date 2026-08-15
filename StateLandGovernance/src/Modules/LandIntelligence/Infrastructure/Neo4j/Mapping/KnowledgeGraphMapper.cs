using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Mapping;

internal static class KnowledgeGraphMapper
{
    public static LandParcelGraphNodeDto ToLandParcelNode(LandParcel parcel) =>
        new(
            parcel.Id,
            parcel.Identifier.CadastralNumber,
            parcel.Identifier.SurveyPlanReference);

    public static AdministrativeAreaGraphNodeDto ToAdministrativeAreaNode(
        Guid id,
        AdministrativeLocation location) =>
        new(
            id,
            location.Province,
            location.District,
            location.DivisionalSecretariat,
            location.GramaNiladhariDivision);

    public static LandCategoryGraphNodeDto ToLandCategoryNode(
        Guid id,
        LandCategory category) =>
        new(
            id,
            category.Type.ToString(),
            category.Type.ToString(),
            category.Description);

    public static LandUseGraphNodeDto ToLandUseNode(Guid id, LandUse landUse) =>
        new(
            id,
            landUse.Type.ToString(),
            landUse.Type.ToString(),
            landUse.Description);

    public static SpatialConstraintGraphNodeDto ToSpatialConstraintNode(SpatialConstraint constraint) =>
        new(
            constraint.Id,
            constraint.Type.ToString(),
            constraint.Description,
            constraint.Severity.ToString());

    public static RegulationGraphNodeDto ToRegulationNode(RegulatoryReference reference) =>
        new(
            reference.Id,
            reference.GazetteNumber,
            reference.Title,
            reference.EffectiveDate,
            reference.Summary);

    public static InfrastructureFeatureGraphNodeDto ToInfrastructureFeatureNode(InfrastructureFeature feature) =>
        new(
            feature.Id,
            feature.Type.ToString(),
            feature.Name,
            feature.DistanceMeters,
            feature.Description);

    public static EnvironmentalAreaGraphNodeDto ToEnvironmentalAreaNode(EnvironmentalRestriction restriction) =>
        new(
            restriction.Id,
            restriction.Type.ToString(),
            restriction.Description,
            restriction.Severity.ToString());

    public static LandRelationshipDto ToRelationshipDto(
        string relationshipType,
        string sourceNodeType,
        object sourceNodeId,
        string targetNodeType,
        object targetNodeId,
        string? description) =>
        new(
            relationshipType,
            sourceNodeType,
            ConvertNodeId(sourceNodeId),
            targetNodeType,
            ConvertNodeId(targetNodeId),
            description);

    private static string ConvertNodeId(object nodeId) => nodeId switch
    {
        Guid guid => guid.ToString(),
        string text => text,
        _ => nodeId.ToString() ?? string.Empty
    };

    public static object ToParameters(LandParcelGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        cadastralNumber = node.CadastralNumber,
        surveyPlanReference = node.SurveyPlanReference
    };

    public static object ToParameters(AdministrativeAreaGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        province = node.Province,
        district = node.District,
        divisionalSecretariat = node.DivisionalSecretariat,
        gramaNiladhariDivision = node.GramaNiladhariDivision
    };

    public static object ToParameters(LandCategoryGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        categoryType = node.CategoryType,
        name = node.Name,
        description = node.Description
    };

    public static object ToParameters(LandUseGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        useType = node.UseType,
        name = node.Name,
        description = node.Description
    };

    public static object ToParameters(SpatialConstraintGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        constraintType = node.ConstraintType,
        description = node.Description,
        severity = node.Severity
    };

    public static object ToParameters(RegulationGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        gazetteNumber = node.GazetteNumber,
        title = node.Title,
        effectiveDate = node.EffectiveDate.ToString("O"),
        summary = node.Summary
    };

    public static object ToParameters(InfrastructureFeatureGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        featureType = node.FeatureType,
        name = node.Name,
        distanceMeters = node.DistanceMeters,
        description = node.Description
    };

    public static object ToParameters(EnvironmentalAreaGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        restrictionType = node.RestrictionType,
        description = node.Description,
        severity = node.Severity
    };

    public static object ToLinkParameters(Guid parcelId, Guid targetId, decimal? distanceMeters = null) => new
    {
        parcelId = parcelId.ToString(),
        targetId = targetId.ToString(),
        distanceMeters
    };
}
