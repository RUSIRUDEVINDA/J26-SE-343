using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Presentation.Models;

public sealed record UpdateLandParcelRequest
{
    public LandUseType? CurrentUseType { get; init; }
    public string? CurrentUseDescription { get; init; }
    public LandCharacteristicsInputDto? Characteristics { get; init; }
    public GeoJsonPolygonDto? BoundaryPolygon { get; init; }
    public IReadOnlyList<SpatialConstraintInputDto>? SpatialConstraints { get; init; }
    public IReadOnlyList<EnvironmentalRestrictionInputDto>? EnvironmentalRestrictions { get; init; }
    public IReadOnlyList<InfrastructureFeatureInputDto>? InfrastructureFeatures { get; init; }
    public IReadOnlyList<RegulatoryReferenceInputDto>? RegulatoryReferences { get; init; }
}
