using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandParcelInputMapper
{
    public static LandCharacteristics? ToCharacteristics(LandCharacteristicsInputDto? input) =>
        input is null
            ? null
            : new LandCharacteristics(
                input.SoilType,
                input.TerrainDescription,
                input.ElevationMeters,
                AttributeProvenanceMapper.ToDomain(input.SoilTypeProvenance),
                AttributeProvenanceMapper.ToDomain(input.TerrainDescriptionProvenance),
                AttributeProvenanceMapper.ToDomain(input.ElevationMetersProvenance));

    public static LandCharacteristics MergeCharacteristics(
        LandCharacteristics? existing,
        LandCharacteristicsInputDto input) =>
        new(
            input.SoilType ?? existing?.SoilType,
            input.TerrainDescription ?? existing?.TerrainDescription,
            input.ElevationMeters ?? existing?.ElevationMeters,
            input.SoilTypeProvenance is not null
                ? AttributeProvenanceMapper.ToDomain(input.SoilTypeProvenance)
                : existing?.SoilTypeProvenance,
            input.TerrainDescriptionProvenance is not null
                ? AttributeProvenanceMapper.ToDomain(input.TerrainDescriptionProvenance)
                : existing?.TerrainDescriptionProvenance,
            input.ElevationMetersProvenance is not null
                ? AttributeProvenanceMapper.ToDomain(input.ElevationMetersProvenance)
                : existing?.ElevationMetersProvenance);

    public static SpatialReference ToSpatialReference(
        double centroidLatitude,
        double centroidLongitude,
        string coordinateSystem,
        string? boundaryReference,
        GeoJsonPolygonDto? boundaryPolygon) =>
        new(
            centroidLatitude,
            centroidLongitude,
            coordinateSystem,
            boundaryReference,
            GeoJsonGeometryMapper.ToGeoBoundary(boundaryPolygon));

    public static IReadOnlyList<SpatialConstraint> ToSpatialConstraints(
        IReadOnlyList<SpatialConstraintInputDto>? inputs) =>
        inputs?.Select(ToSpatialConstraint).ToList() ?? [];

    public static IReadOnlyList<EnvironmentalRestriction> ToEnvironmentalRestrictions(
        IReadOnlyList<EnvironmentalRestrictionInputDto>? inputs) =>
        inputs?.Select(ToEnvironmentalRestriction).ToList() ?? [];

    public static IReadOnlyList<InfrastructureFeature> ToInfrastructureFeatures(
        IReadOnlyList<InfrastructureFeatureInputDto>? inputs) =>
        inputs?.Select(ToInfrastructureFeature).ToList() ?? [];

    public static IReadOnlyList<RegulatoryReference> ToRegulatoryReferences(
        IReadOnlyList<RegulatoryReferenceInputDto>? inputs) =>
        inputs?.Select(ToRegulatoryReference).ToList() ?? [];

    public static void ApplyChildCollections(LandParcel parcel, CreateLandParcelCommand command)
    {
        foreach (var constraint in ToSpatialConstraints(command.SpatialConstraints))
        {
            parcel.AddSpatialConstraint(constraint);
        }

        foreach (var restriction in ToEnvironmentalRestrictions(command.EnvironmentalRestrictions))
        {
            parcel.AddEnvironmentalRestriction(restriction);
        }

        foreach (var feature in ToInfrastructureFeatures(command.InfrastructureFeatures))
        {
            parcel.AddInfrastructureFeature(feature);
        }

        foreach (var reference in ToRegulatoryReferences(command.RegulatoryReferences))
        {
            parcel.AddRegulatoryReference(reference);
        }
    }

    public static void ApplyUpdate(LandParcel parcel, UpdateLandParcelCommand command)
    {
        if (command.CurrentUseType is not null)
        {
            parcel.UpdateCurrentUse(new LandUse(command.CurrentUseType.Value, command.CurrentUseDescription));
        }

        if (command.Characteristics is not null)
        {
            parcel.UpdateCharacteristics(MergeCharacteristics(parcel.Characteristics, command.Characteristics));
        }

        if (command.BoundaryPolygon is not null)
        {
            var boundary = GeoJsonGeometryMapper.ToGeoBoundary(command.BoundaryPolygon);
            parcel.UpdateSpatial(new SpatialReference(
                parcel.Spatial.CentroidLatitude,
                parcel.Spatial.CentroidLongitude,
                parcel.Spatial.CoordinateSystem,
                parcel.Spatial.BoundaryReference,
                boundary));
        }

        if (command.SpatialConstraints is not null)
        {
            parcel.ReplaceSpatialConstraints(ToSpatialConstraints(command.SpatialConstraints));
        }

        if (command.EnvironmentalRestrictions is not null)
        {
            parcel.ReplaceEnvironmentalRestrictions(ToEnvironmentalRestrictions(command.EnvironmentalRestrictions));
        }

        if (command.InfrastructureFeatures is not null)
        {
            parcel.ReplaceInfrastructureFeatures(ToInfrastructureFeatures(command.InfrastructureFeatures));
        }

        if (command.RegulatoryReferences is not null)
        {
            parcel.ReplaceRegulatoryReferences(ToRegulatoryReferences(command.RegulatoryReferences));
        }
    }

    private static SpatialConstraint ToSpatialConstraint(SpatialConstraintInputDto input) =>
        new(
            input.Type,
            input.Description,
            input.Severity,
            GeoJsonGeometryMapper.ToGeoBoundary(input.Geometry),
            input.Id);

    private static EnvironmentalRestriction ToEnvironmentalRestriction(EnvironmentalRestrictionInputDto input) =>
        new(
            input.Type,
            input.Description,
            input.Severity,
            AttributeProvenanceMapper.ToDomain(input.DataProvenance),
            input.Id);

    private static InfrastructureFeature ToInfrastructureFeature(InfrastructureFeatureInputDto input)
    {
        GeoCoordinate? location = input.LocationLatitude is not null && input.LocationLongitude is not null
            ? new GeoCoordinate(input.LocationLatitude.Value, input.LocationLongitude.Value)
            : null;

        return new InfrastructureFeature(
            input.Type,
            input.Name,
            input.DistanceMeters,
            input.Description,
            AttributeProvenanceMapper.ToDomain(input.DistanceProvenance),
            location,
            input.Id);
    }

    private static RegulatoryReference ToRegulatoryReference(RegulatoryReferenceInputDto input) =>
        new(
            input.GazetteNumber,
            input.Title,
            input.EffectiveDate,
            input.Summary,
            AttributeProvenanceMapper.ToDomain(input.DataProvenance),
            input.Id);
}
