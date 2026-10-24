using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IProjectLeaseReadyDesignationReadRepository
{
    Task<ProjectLeaseReadyDesignationRecord?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord>> GetByParcelIdsAsync(
        IReadOnlyList<Guid> landParcelIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectLeaseReadyDesignationRecord>> ListDesignatedAsync(
        CancellationToken cancellationToken = default);

    Task<int> CountDesignatedAsync(CancellationToken cancellationToken = default);

    Task<int> CountTotalParcelsAsync(CancellationToken cancellationToken = default);
}
