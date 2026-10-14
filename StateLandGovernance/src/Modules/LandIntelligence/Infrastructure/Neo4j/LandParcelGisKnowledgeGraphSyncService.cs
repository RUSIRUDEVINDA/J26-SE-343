using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

public sealed class LandParcelGisKnowledgeGraphSyncService : ILandParcelGisKnowledgeGraphSyncService
{
    private readonly IGisGraphSyncRequestBuilder _syncRequestBuilder;
    private readonly IKnowledgeGraphService _knowledgeGraphService;
    private readonly ILogger<LandParcelGisKnowledgeGraphSyncService> _logger;

    public LandParcelGisKnowledgeGraphSyncService(
        IGisGraphSyncRequestBuilder syncRequestBuilder,
        IKnowledgeGraphService knowledgeGraphService,
        ILogger<LandParcelGisKnowledgeGraphSyncService> logger)
    {
        _syncRequestBuilder = syncRequestBuilder;
        _knowledgeGraphService = knowledgeGraphService;
        _logger = logger;
    }

    public async Task SyncAsync(Guid parcelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = await _syncRequestBuilder.BuildSyncRequestAsync(parcelId, cancellationToken);
            if (request is null)
            {
                throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");
            }

            await _knowledgeGraphService.SyncGisDerivedParcelIntelligenceAsync(request, cancellationToken);

            _logger.LogInformation(
                "Synchronized GIS-derived intelligence for parcel {ParcelId} to the knowledge graph with status {OverallStatus}.",
                parcelId,
                request.OverallStatus);
        }
        catch (ServiceConfigurationException ex)
        {
            _logger.LogWarning(
                ex,
                "GIS knowledge graph synchronization skipped for parcel {ParcelId} because Neo4j is not configured.",
                parcelId);
        }
        catch (Neo4jException ex)
        {
            _logger.LogWarning(
                ex,
                "GIS knowledge graph synchronization failed for parcel {ParcelId} because Neo4j is unavailable.",
                parcelId);
        }
    }
}
