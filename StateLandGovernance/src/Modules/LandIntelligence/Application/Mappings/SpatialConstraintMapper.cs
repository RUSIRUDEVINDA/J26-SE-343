using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class SpatialConstraintMapper
{
    public static SpatialConstraintDto ToDto(SpatialConstraint constraint, Guid landParcelId) =>
        new(
            constraint.Id,
            landParcelId,
            constraint.Type,
            constraint.Description,
            constraint.Severity);
}
