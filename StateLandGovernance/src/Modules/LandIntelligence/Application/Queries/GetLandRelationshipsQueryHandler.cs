using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetLandRelationshipsQueryHandler
    : IQueryHandler<GetLandRelationshipsQuery, LandRelationshipsResponse>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly IKnowledgeGraphService _knowledgeGraphService;

    public GetLandRelationshipsQueryHandler(
        ILandParcelRepository landParcelRepository,
        IKnowledgeGraphService knowledgeGraphService)
    {
        _landParcelRepository = landParcelRepository;
        _knowledgeGraphService = knowledgeGraphService;
    }

    public async Task<LandRelationshipsResponse> HandleAsync(
        GetLandRelationshipsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.LandParcelId == Guid.Empty)
        {
            throw new ValidationException(["Land parcel identifier is required."]);
        }

        _ = await _landParcelRepository.GetByIdAsync(query.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(query.LandParcelId);

        var relationships = await _knowledgeGraphService.GetRelationshipsAsync(
            query.LandParcelId,
            cancellationToken);

        return new LandRelationshipsResponse(query.LandParcelId, relationships);
    }
}
