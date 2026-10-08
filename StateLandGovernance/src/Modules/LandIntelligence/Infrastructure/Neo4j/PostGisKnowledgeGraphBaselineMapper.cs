using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Mapping;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

internal static class PostGisKnowledgeGraphBaselineMapper
{
    public static LandParcelGisGraphIntelligenceDto ToGisGraphIntelligence(
        GisDerivedParcelIntelligenceGraphSyncRequest request)
    {
        var administrative = request.Administrative;

        return new LandParcelGisGraphIntelligenceDto
        {
            ParcelId = request.ParcelId,
            DetectedProvince = administrative?.ProvinceName,
            ProvinceReferenceId = administrative?.ProvinceReferenceId,
            DetectedDistrict = administrative?.DistrictName,
            DistrictReferenceId = administrative?.DistrictReferenceId,
            NearestRoad = request.Road,
            NearestWater = request.Water,
            DerivedSoil = request.Soil,
            ConservationAreas = request.ConservationAreas
        };
    }

    public static IReadOnlyList<LandRelationshipDto> ToRelationships(
        LandParcel parcel,
        GisDerivedParcelIntelligenceGraphSyncRequest? gisSyncRequest)
    {
        var parcelId = parcel.Id.ToString();
        var relationships = new List<LandRelationshipDto>();

        var adminAreaId = KnowledgeGraphReferenceResolver.ResolveAdministrativeAreaId(parcel.Location);
        relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
            GraphRelationshipTypes.LocatedIn,
            "LandParcel",
            parcelId,
            "AdministrativeArea",
            adminAreaId,
            parcel.Location.District));

        var categoryId = KnowledgeGraphReferenceResolver.ResolveCategoryId(parcel);
        relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
            GraphRelationshipTypes.HasCategory,
            "LandParcel",
            parcelId,
            "LandCategory",
            categoryId,
            parcel.Category.Type.ToString()));

        if (parcel.CurrentUse is not null)
        {
            var landUseId = KnowledgeGraphReferenceResolver.ResolveLandUseId(parcel);
            if (landUseId is not null)
            {
                relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                    GraphRelationshipTypes.HasUse,
                    "LandParcel",
                    parcelId,
                    "LandUse",
                    landUseId.Value,
                    parcel.CurrentUse.Type.ToString()));
            }
        }

        foreach (var constraint in parcel.SpatialConstraints)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.HasRestriction,
                "LandParcel",
                parcelId,
                "SpatialConstraint",
                constraint.Id,
                constraint.Description));
        }

        foreach (var reference in parcel.RegulatoryReferences)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                "SUBJECT_TO",
                "LandParcel",
                parcelId,
                "Regulation",
                reference.Id,
                reference.Title));
        }

        foreach (var feature in parcel.InfrastructureFeatures)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.Near,
                "LandParcel",
                parcelId,
                "InfrastructureFeature",
                feature.Id,
                feature.Name));
        }

        foreach (var restriction in parcel.EnvironmentalRestrictions)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.RelatedTo,
                "LandParcel",
                parcelId,
                "EnvironmentalArea",
                restriction.Id,
                restriction.Description));
        }

        if (gisSyncRequest is null)
        {
            return relationships
                .OrderBy(relationship => relationship.RelationshipType, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.TargetNodeType, StringComparer.Ordinal)
                .ToList();
        }

        if (gisSyncRequest.Administrative is { } administrative)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.LocatedIn,
                "LandParcel",
                parcelId,
                "Province",
                administrative.ProvinceReferenceId,
                administrative.ProvinceName));

            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.LocatedIn,
                "LandParcel",
                parcelId,
                "District",
                administrative.DistrictReferenceId,
                administrative.DistrictName));
        }

        if (gisSyncRequest.Road is { } road)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.NearRoad,
                "LandParcel",
                parcelId,
                "Road",
                road.RoadReferenceId,
                road.RoadName));
        }

        if (gisSyncRequest.Water is { } water)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.NearWater,
                "LandParcel",
                parcelId,
                "WaterFeature",
                water.WaterReferenceId,
                water.FeatureName));
        }

        if (gisSyncRequest.Soil is { } soil)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.HasDerivedSoil,
                "LandParcel",
                parcelId,
                "SoilGroup",
                soil.SoilGroupReferenceId,
                soil.SoilGroupName));
        }

        foreach (var conservation in gisSyncRequest.ConservationAreas)
        {
            relationships.Add(KnowledgeGraphMapper.ToRelationshipDto(
                GraphRelationshipTypes.IntersectsConservationArea,
                "LandParcel",
                parcelId,
                "ConservationArea",
                conservation.ConservationAreaReferenceId,
                conservation.ConservationAreaName));
        }

        return relationships
            .OrderBy(relationship => relationship.RelationshipType, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetNodeType, StringComparer.Ordinal)
            .ToList();
    }
}
