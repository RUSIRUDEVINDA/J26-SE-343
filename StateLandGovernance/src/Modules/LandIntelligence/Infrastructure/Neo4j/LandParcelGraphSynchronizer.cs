using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

internal sealed class LandParcelGraphSynchronizer : ILandParcelGraphSynchronizer
{
    private readonly IKnowledgeGraphService _knowledgeGraphService;
    private readonly ILogger<LandParcelGraphSynchronizer> _logger;

    public LandParcelGraphSynchronizer(
        IKnowledgeGraphService knowledgeGraphService,
        ILogger<LandParcelGraphSynchronizer> logger)
    {
        _knowledgeGraphService = knowledgeGraphService;
        _logger = logger;
    }

    public async Task SynchronizeAfterPersistAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        try
        {
            await _knowledgeGraphService.SyncLandParcelGraphAsync(parcel, cancellationToken);
            _logger.LogInformation(
                "Land parcel {ParcelId} successfully synchronized to knowledge graph.",
                parcel.Id);
        }
        catch (ServiceConfigurationException ex)
        {
            _logger.LogWarning(
                ex,
                "Knowledge graph synchronization skipped for parcel {ParcelId} because Neo4j is not configured.",
                parcel.Id);
        }
        catch (Neo4jException ex)
        {
            _logger.LogWarning(
                ex,
                "Knowledge graph synchronization failed for parcel {ParcelId} because Neo4j is unavailable.",
                parcel.Id);
        }
    }

    public async Task RemoveAfterDeleteAsync(Guid parcelId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _knowledgeGraphService.DeleteLandParcelGraphAsync(parcelId, cancellationToken);
            _logger.LogInformation(
                "Land parcel {ParcelId} successfully removed from knowledge graph.",
                parcelId);
        }
        catch (ServiceConfigurationException ex)
        {
            _logger.LogWarning(
                ex,
                "Knowledge graph deletion skipped for parcel {ParcelId} because Neo4j is not configured.",
                parcelId);
        }
        catch (Neo4jException ex)
        {
            _logger.LogWarning(
                ex,
                "Knowledge graph deletion failed for parcel {ParcelId} because Neo4j is unavailable.",
                parcelId);
        }
    }
}
