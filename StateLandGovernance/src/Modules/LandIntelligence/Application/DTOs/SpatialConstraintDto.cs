using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record SpatialConstraintDto(
    Guid Id,
    Guid LandParcelId,
    SpatialConstraintType Type,
    string Description,
    RestrictionSeverity Severity);
