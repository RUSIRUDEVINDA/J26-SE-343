using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetSpatialConstraintsByParcelIdQueryHandler
    : IQueryHandler<GetSpatialConstraintsByParcelIdQuery, IReadOnlyList<SpatialConstraintDto>>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly ISpatialConstraintRepository _spatialConstraintRepository;
    private readonly ISpatialAnalysisService _spatialAnalysisService;

    public GetSpatialConstraintsByParcelIdQueryHandler(
        ILandParcelRepository landParcelRepository,
        ISpatialConstraintRepository spatialConstraintRepository,
        ISpatialAnalysisService spatialAnalysisService)
    {
        _landParcelRepository = landParcelRepository;
        _spatialConstraintRepository = spatialConstraintRepository;
        _spatialAnalysisService = spatialAnalysisService;
    }

    public async Task<IReadOnlyList<SpatialConstraintDto>> HandleAsync(
        GetSpatialConstraintsByParcelIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.LandParcelId == Guid.Empty)
        {
            throw new ValidationException(["Land parcel identifier is required."]);
        }

        _ = await _landParcelRepository.GetByIdAsync(query.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(query.LandParcelId);

        var storedConstraints = await _spatialConstraintRepository.GetByParcelIdAsync(
            query.LandParcelId,
            cancellationToken);

        if (storedConstraints.Count > 0)
        {
            return storedConstraints
                .Select(constraint => SpatialConstraintMapper.ToDto(constraint, query.LandParcelId))
                .ToList();
        }

        return await _spatialAnalysisService.AnalyzeConstraintsAsync(
            query.LandParcelId,
            cancellationToken);
    }
}
