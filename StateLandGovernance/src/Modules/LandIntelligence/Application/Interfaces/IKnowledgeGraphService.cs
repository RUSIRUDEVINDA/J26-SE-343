using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Contract for land knowledge graph relationship retrieval (Neo4j implementation deferred).
/// </summary>
public interface IKnowledgeGraphService
{
    Task<IReadOnlyList<LandRelationshipDto>> GetRelationshipsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
