using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class ParcelAttributeProvenanceResolver
{
    public static AttributeProvenance ResolveOrUnknown(AttributeProvenance? provenance) =>
        provenance ?? AttributeProvenance.Unknown();

    public static AttributeProvenance? ResolveCharacteristicProvenance(LandParcel parcel, string? attributePath)
    {
        if (parcel.Characteristics is null || string.IsNullOrWhiteSpace(attributePath))
        {
            return null;
        }

        return attributePath.Trim().ToLowerInvariant() switch
        {
            "characteristics.soiltype" => parcel.Characteristics.SoilTypeProvenance,
            "characteristics.terraindescription" => parcel.Characteristics.TerrainDescriptionProvenance,
            "characteristics.elevationmeters" => parcel.Characteristics.ElevationMetersProvenance,
            _ => null
        };
    }

    public static AttributeProvenance ResolveEnvironmentalProvenance(LandParcel parcel)
    {
        var restriction = parcel.EnvironmentalRestrictions
            .OrderByDescending(r => r.Severity)
            .FirstOrDefault();

        return restriction is null
            ? AttributeProvenance.Unknown()
            : ResolveOrUnknown(restriction.DataProvenance);
    }

    public static AttributeProvenance ResolveRegulatoryProvenance(LandParcel parcel)
    {
        var reference = parcel.RegulatoryReferences.FirstOrDefault();
        return reference is null
            ? AttributeProvenance.Unknown()
            : ResolveOrUnknown(reference.DataProvenance);
    }

    public static AttributeProvenance ResolveRoadDistanceProvenance(InfrastructureFeature? feature) =>
        feature is null
            ? AttributeProvenance.Unknown("Road distance")
            : ResolveOrUnknown(feature.DistanceProvenance);
}
