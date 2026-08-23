using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Presentation.Models;

namespace StateLandGovernance.LandIntelligence.Presentation.Mappings;

internal static class LandParcelRequestMapper
{
    public static CreateLandParcelCommand ToCreateCommand(CreateLandParcelRequest request) =>
        new()
        {
            CadastralNumber = request.CadastralNumber,
            SurveyPlanReference = request.SurveyPlanReference,
            CategoryType = request.CategoryType,
            CategoryDescription = request.CategoryDescription,
            AreaValue = request.AreaValue,
            AreaUnit = request.AreaUnit,
            Province = request.Province,
            District = request.District,
            DivisionalSecretariat = request.DivisionalSecretariat,
            GramaNiladhariDivision = request.GramaNiladhariDivision,
            CentroidLatitude = request.CentroidLatitude,
            CentroidLongitude = request.CentroidLongitude,
            CoordinateSystem = request.CoordinateSystem,
            BoundaryReference = request.BoundaryReference,
            BoundaryPolygon = request.BoundaryPolygon,
            CurrentUseType = request.CurrentUseType,
            CurrentUseDescription = request.CurrentUseDescription,
            Characteristics = request.Characteristics,
            SpatialConstraints = request.SpatialConstraints,
            EnvironmentalRestrictions = request.EnvironmentalRestrictions,
            InfrastructureFeatures = request.InfrastructureFeatures,
            RegulatoryReferences = request.RegulatoryReferences
        };

    public static UpdateLandParcelCommand ToUpdateCommand(Guid landParcelId, UpdateLandParcelRequest request) =>
        new()
        {
            LandParcelId = landParcelId,
            CurrentUseType = request.CurrentUseType,
            CurrentUseDescription = request.CurrentUseDescription,
            Characteristics = request.Characteristics,
            BoundaryPolygon = request.BoundaryPolygon,
            SpatialConstraints = request.SpatialConstraints,
            EnvironmentalRestrictions = request.EnvironmentalRestrictions,
            InfrastructureFeatures = request.InfrastructureFeatures,
            RegulatoryReferences = request.RegulatoryReferences
        };
}
