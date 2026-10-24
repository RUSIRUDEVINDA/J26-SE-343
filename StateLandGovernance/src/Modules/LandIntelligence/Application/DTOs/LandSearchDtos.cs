using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record LandParcelDto(
    Guid Id,
    ParcelIdentifierDto Identifier,
    LandCategoryDto Category,
    LandUseDto? CurrentUse,
    LandAreaDto Area,
    AdministrativeLocationDto Location,
    SpatialReferenceDto Spatial,
    LandCharacteristicsDto? Characteristics,
    int SpatialConstraintCount,
    int EnvironmentalRestrictionCount,
    int InfrastructureFeatureCount,
    int RegulatoryReferenceCount,
    CadastralSourceMetadataDto? CadastralSource = null,
    LandParcelAvailabilityDto? Availability = null,
    ParcelGisEnrichmentSummaryDto? GisEnrichment = null,
    ParcelGnDivisionMappingDto? GnDivisionMapping = null);

public sealed record LandSearchRequest
{
    public string? Province { get; init; }
    public string? District { get; init; }
    public string? DivisionalSecretariat { get; init; }
    /// <summary>Exact match on GN division name (parcel location and/or Colombo GN mapping workbook).</summary>
    public string? GramaNiladhariDivision { get; init; }
    /// <summary>Exact match (trimmed, case-insensitive) on cadastral number. Ordinary users search by this, not UUID.</summary>
    public string? CadastralNumber { get; init; }
    public LandCategoryType? CategoryType { get; init; }
    public LandUseType? CurrentUseType { get; init; }
    /// <summary>Exact match (trimmed, case-insensitive) on source-reported land-use code or English label.</summary>
    public string? SourceReportedLandUse { get; init; }
    /// <summary>Canonical land use / project research group; matches original cadastral label and/or GIS classification.</summary>
    public string? ResearchLandCoverGroup { get; init; }
    /// <summary>Exact match (trimmed, case-insensitive) on GIS-derived soil group name.</summary>
    public string? DerivedSoilGroup { get; init; }
    public decimal? MinArea { get; init; }
    public decimal? MaxArea { get; init; }
    public double? MinLatitude { get; init; }
    public double? MaxLatitude { get; init; }
    public double? MinLongitude { get; init; }
    public double? MaxLongitude { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record LandSearchResultDto(
    Guid Id,
    string CadastralNumber,
    string Province,
    string District,
    LandCategoryType CategoryType,
    LandUseType? CurrentUseType,
    decimal AreaValue,
    AreaUnit AreaUnit,
    double CentroidLatitude,
    double CentroidLongitude,
    CadastralSourceMetadataDto? CadastralSource = null,
    LandParcelAvailabilityDto? Availability = null);

public sealed record LandSearchResponse(
    IReadOnlyList<LandSearchResultDto> Results,
    int Page,
    int PageSize,
    int TotalCount);
