using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ISpatialConstraintRepository
{
    Task<IReadOnlyList<SpatialConstraint>> GetByParcelIdAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
