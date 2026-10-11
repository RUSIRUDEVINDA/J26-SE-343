namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public sealed class GetVerifiedFactSnapshotByIdQueryHandler : IQueryHandler<GetVerifiedFactSnapshotByIdQuery, VerifiedFactSnapshotDto?>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;

    public GetVerifiedFactSnapshotByIdQueryHandler(IDocumentAnalysisRepository analysisRepository)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
    }

    public async Task<VerifiedFactSnapshotDto?> HandleAsync(
        GetVerifiedFactSnapshotByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var analysis = await _analysisRepository.GetByIdAsync(
            new DocumentAnalysisId(query.DocumentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            return null;
        }

        var snapshot = analysis.VerifiedFactSnapshots.FirstOrDefault(s => s.Id.Value == query.SnapshotId);
        return snapshot != null ? FactVerificationMapper.ToDto(snapshot) : null;
    }
}
