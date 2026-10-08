namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed class GisReferenceDataValidationResult
{
    public required bool IsValid { get; init; }

    public required IReadOnlyDictionary<string, int> TableCounts { get; init; }

    public required IReadOnlyDictionary<string, GisReferenceTableValidationSummary> TableSummaries { get; init; }

    public required GisReferenceHambantotaSpatialValidation HambantotaSpatialValidation { get; init; }

    public required GisReferenceNearestRoadDistanceResult NearestRoadDistance { get; init; }

    public required IReadOnlyList<string> Issues { get; init; }
}

public sealed class GisReferenceTableValidationSummary
{
    public required string GeometryColumn { get; init; }

    public required string DominantGeometryType { get; init; }

    public required int TotalRows { get; init; }

    public required int NullGeometryCount { get; init; }

    public required int WrongSridCount { get; init; }

    public required int InvalidGeometryCount { get; init; }

    public required int UnexpectedGeometryTypeCount { get; init; }

    public required bool GistIndexExists { get; init; }
}

public sealed class GisReferenceHambantotaSpatialValidation
{
    public required bool HambantotaDistrictExists { get; init; }

    public required bool SouthernProvinceExists { get; init; }

    public required bool DistrictIntersectsProvince { get; init; }

    public required int RoadsOutsideHambantotaCount { get; init; }

    public required int WaterFeaturesOutsideHambantotaCount { get; init; }

    public required int SoilGroupsOutsideHambantotaCount { get; init; }

    public required int SoilConservationAreasOutsideHambantotaCount { get; init; }

    public required int ErosionObservationsOutsideHambantotaCount { get; init; }

    public required int ExpresswaysIntersectingHambantotaCount { get; init; }

    public required int WaterFeaturesIntersectingHambantotaCount { get; init; }

    public required int SoilGroupsIntersectingHambantotaCount { get; init; }

    public required int SoilConservationAreasIntersectingHambantotaCount { get; init; }

    public required int ErosionObservationsIntersectingHambantotaCount { get; init; }
}

public sealed class GisReferenceNearestRoadDistanceResult
{
    public required double TestLatitude { get; init; }

    public required double TestLongitude { get; init; }

    public required string TestPointSource { get; init; }

    public required bool RoadFound { get; init; }

    public required double? DistanceMeters { get; init; }

    public required string? NearestRoadName { get; init; }
}
