using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Best-effort knowledge graph projection after authoritative PostgreSQL/PostGIS persistence.
/// </summary>
public interface ILandParcelGraphSynchronizer
{
    Task SynchronizeAfterPersistAsync(LandParcel parcel, CancellationToken cancellationToken = default);

    Task RemoveAfterDeleteAsync(Guid parcelId, CancellationToken cancellationToken = default);
}
