using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Presentation.Models;

namespace StateLandGovernance.LandIntelligence.Presentation.Mappings;

internal static class LandParcelRequestMapper
{
    public static CreateLandParcelCommand ToCreateCommand(CreateLandParcelRequest request) =>
        new(
            request.CadastralNumber,
            request.SurveyPlanReference,
            request.CategoryType,
            request.CategoryDescription,
            request.AreaValue,
            request.AreaUnit,
            request.Province,
            request.District,
            request.DivisionalSecretariat,
            request.GramaNiladhariDivision,
            request.CentroidLatitude,
            request.CentroidLongitude,
            request.CoordinateSystem,
            request.BoundaryReference,
            request.CurrentUseType,
            request.CurrentUseDescription,
            request.SoilType,
            request.TerrainDescription,
            request.ElevationMeters);

    public static UpdateLandParcelCommand ToUpdateCommand(Guid landParcelId, UpdateLandParcelRequest request) =>
        new(
            landParcelId,
            request.CurrentUseType,
            request.CurrentUseDescription,
            request.SoilType,
            request.TerrainDescription,
            request.ElevationMeters);
}
