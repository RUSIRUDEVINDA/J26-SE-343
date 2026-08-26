namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Synchronizes H10-persisted GIS-derived parcel intelligence into the Neo4j knowledge graph.
/// </summary>
public interface ILandParcelGisKnowledgeGraphSyncService
{
    Task SyncAsync(Guid parcelId, CancellationToken cancellationToken = default);
}
