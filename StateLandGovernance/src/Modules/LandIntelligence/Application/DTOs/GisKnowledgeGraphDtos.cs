namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record ProvinceGraphNodeDto(
    Guid Id,
    string Name,
    string SourceName);

public sealed record DistrictGraphNodeDto(
    Guid Id,
    string Name,
    string SourceName);

public sealed record RoadGraphNodeDto(
    Guid Id,
    string? Name,
    string RoadType,
    string SourceName);

public sealed record WaterFeatureGraphNodeDto(
    Guid Id,
    string? Name,
    string FeatureType,
    string SourceName);

public sealed record SoilGroupGraphNodeDto(
    Guid Id,
    string Name,
    string SourceName);

public sealed record ConservationAreaGraphNodeDto(
    Guid Id,
    string Name,
    string SourceName);

public sealed record GisDerivedAdministrativeGraphSync(
    Guid ProvinceReferenceId,
    string ProvinceName,
    Guid DistrictReferenceId,
    string DistrictName,
    string SourceName,
    DateTimeOffset DerivedAt);

public sealed record GisDerivedRoadGraphSync(
    Guid RoadReferenceId,
    string? RoadName,
    string RoadType,
    decimal DistanceMeters,
    string SourceName,
    DateTimeOffset DerivedAt);

public sealed record GisDerivedWaterGraphSync(
    Guid WaterReferenceId,
    string? FeatureName,
    string FeatureType,
    decimal DistanceMeters,
    string SourceName,
    DateTimeOffset DerivedAt);

public sealed record GisDerivedSoilGraphSync(
    Guid SoilGroupReferenceId,
    string SoilGroupName,
    decimal? OverlapPercentage,
    string SourceName,
    DateTimeOffset DerivedAt);

public sealed record GisDerivedConservationGraphSync(
    Guid ConservationAreaReferenceId,
    string ConservationAreaName,
    decimal? OverlapPercentage,
    string SourceName,
    DateTimeOffset DerivedAt);

public sealed record GisDerivedParcelIntelligenceGraphSyncRequest
{
    public required Guid ParcelId { get; init; }

    public required string CadastralNumber { get; init; }

    public string? SurveyPlanReference { get; init; }

    public required LandParcelGisEnrichmentOverallStatus OverallStatus { get; init; }

    public GisDerivedAdministrativeGraphSync? Administrative { get; init; }

    public GisDerivedRoadGraphSync? Road { get; init; }

    public GisDerivedWaterGraphSync? Water { get; init; }

    public GisDerivedSoilGraphSync? Soil { get; init; }

    public required IReadOnlyList<GisDerivedConservationGraphSync> ConservationAreas { get; init; }
}

public sealed record LandParcelGisGraphIntelligenceDto
{
    public required Guid ParcelId { get; init; }

    public string? DetectedProvince { get; init; }

    public Guid? ProvinceReferenceId { get; init; }

    public string? DetectedDistrict { get; init; }

    public Guid? DistrictReferenceId { get; init; }

    public GisDerivedRoadGraphSync? NearestRoad { get; init; }

    public GisDerivedWaterGraphSync? NearestWater { get; init; }

    public GisDerivedSoilGraphSync? DerivedSoil { get; init; }

    public required IReadOnlyList<GisDerivedConservationGraphSync> ConservationAreas { get; init; }
}
