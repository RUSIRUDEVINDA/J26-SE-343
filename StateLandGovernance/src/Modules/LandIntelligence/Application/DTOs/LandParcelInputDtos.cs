using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record LandCharacteristicsInputDto
{
    public string? SoilType { get; init; }
    public string? TerrainDescription { get; init; }
    public decimal? ElevationMeters { get; init; }
    public AttributeProvenanceDto? SoilTypeProvenance { get; init; }
    public AttributeProvenanceDto? TerrainDescriptionProvenance { get; init; }
    public AttributeProvenanceDto? ElevationMetersProvenance { get; init; }
}

public sealed record SpatialConstraintInputDto
{
    public Guid? Id { get; init; }
    public SpatialConstraintType Type { get; init; }
    public required string Description { get; init; }
    public RestrictionSeverity Severity { get; init; }
    public GeoJsonPolygonDto? Geometry { get; init; }
}

public sealed record EnvironmentalRestrictionInputDto
{
    public Guid? Id { get; init; }
    public EnvironmentalRestrictionType Type { get; init; }
    public required string Description { get; init; }
    public RestrictionSeverity Severity { get; init; }
    public AttributeProvenanceDto? DataProvenance { get; init; }
}

public sealed record InfrastructureFeatureInputDto
{
    public Guid? Id { get; init; }
    public InfrastructureFeatureType Type { get; init; }
    public required string Name { get; init; }
    public decimal? DistanceMeters { get; init; }
    public string? Description { get; init; }
    public double? LocationLatitude { get; init; }
    public double? LocationLongitude { get; init; }
    public AttributeProvenanceDto? DistanceProvenance { get; init; }
}

public sealed record RegulatoryReferenceInputDto
{
    public Guid? Id { get; init; }
    public required string GazetteNumber { get; init; }
    public required string Title { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public string? Summary { get; init; }
    public AttributeProvenanceDto? DataProvenance { get; init; }
}
