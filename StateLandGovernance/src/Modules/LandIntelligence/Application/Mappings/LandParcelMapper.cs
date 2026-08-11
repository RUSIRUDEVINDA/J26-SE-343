using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandParcelMapper
{
    public static LandParcelDto ToDto(LandParcel parcel) =>
        new(
            parcel.Id,
            ToIdentifierDto(parcel.Identifier),
            ToCategoryDto(parcel.Category),
            parcel.CurrentUse is null ? null : ToLandUseDto(parcel.CurrentUse),
            ToAreaDto(parcel.Area),
            ToLocationDto(parcel.Location),
            ToSpatialDto(parcel.Spatial),
            parcel.Characteristics is null ? null : ToCharacteristicsDto(parcel.Characteristics),
            parcel.SpatialConstraints.Count,
            parcel.EnvironmentalRestrictions.Count,
            parcel.InfrastructureFeatures.Count,
            parcel.RegulatoryReferences.Count);

    public static LandSearchResultDto ToSearchResultDto(LandParcel parcel) =>
        new(
            parcel.Id,
            parcel.Identifier.CadastralNumber,
            parcel.Location.Province,
            parcel.Location.District,
            parcel.Category.Type,
            parcel.CurrentUse?.Type,
            parcel.Area.Value,
            parcel.Area.Unit,
            parcel.Spatial.CentroidLatitude,
            parcel.Spatial.CentroidLongitude);

    public static LandParcel ToEntity(CreateLandParcelCommand command)
    {
        var identifier = new ParcelIdentifier(command.CadastralNumber, command.SurveyPlanReference);
        var category = new LandCategory(command.CategoryType, command.CategoryDescription);
        var area = new LandArea(command.AreaValue, command.AreaUnit);
        var location = new AdministrativeLocation(
            command.Province,
            command.District,
            command.DivisionalSecretariat,
            command.GramaNiladhariDivision);
        var spatial = new SpatialReference(
            command.CentroidLatitude,
            command.CentroidLongitude,
            command.CoordinateSystem,
            command.BoundaryReference);

        LandUse? currentUse = command.CurrentUseType is null
            ? null
            : new LandUse(command.CurrentUseType.Value, command.CurrentUseDescription);

        LandCharacteristics? characteristics = command.SoilType is null
            && command.TerrainDescription is null
            && command.ElevationMeters is null
            ? null
            : new LandCharacteristics(command.SoilType, command.TerrainDescription, command.ElevationMeters);

        return new LandParcel(identifier, category, area, location, spatial, currentUse, characteristics);
    }

    private static ParcelIdentifierDto ToIdentifierDto(ParcelIdentifier identifier) =>
        new(identifier.CadastralNumber, identifier.SurveyPlanReference);

    private static LandCategoryDto ToCategoryDto(LandCategory category) =>
        new(category.Type, category.Description);

    private static LandUseDto ToLandUseDto(LandUse landUse) =>
        new(landUse.Type, landUse.Description);

    private static LandAreaDto ToAreaDto(LandArea area) =>
        new(area.Value, area.Unit);

    private static AdministrativeLocationDto ToLocationDto(AdministrativeLocation location) =>
        new(
            location.Province,
            location.District,
            location.DivisionalSecretariat,
            location.GramaNiladhariDivision);

    private static SpatialReferenceDto ToSpatialDto(SpatialReference spatial) =>
        new(
            spatial.CentroidLatitude,
            spatial.CentroidLongitude,
            spatial.CoordinateSystem,
            spatial.BoundaryReference);

    private static LandCharacteristicsDto ToCharacteristicsDto(LandCharacteristics characteristics) =>
        new(
            characteristics.SoilType,
            characteristics.TerrainDescription,
            characteristics.ElevationMeters);
}
