namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetCurrentVerifiedFactsForCaseQueryHandler : IQueryHandler<GetCurrentVerifiedFactsForCaseQuery, VerifiedFactSnapshotDto?>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IDocumentAnalysisRepository _analysisRepository;

    public GetCurrentVerifiedFactsForCaseQueryHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IDocumentAnalysisRepository analysisRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
    }

    public async Task<VerifiedFactSnapshotDto?> HandleAsync(
        GetCurrentVerifiedFactsForCaseQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(query.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null || leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty)
        {
            return null;
        }

        var snapshot = await _analysisRepository.GetSnapshotByIdAsync(
            new VerifiedFactSnapshotId(leaseCase.CurrentVerifiedFactSnapshotId),
            cancellationToken);

        return snapshot != null ? FactVerificationMapper.ToDto(snapshot) : null;
    }
}
