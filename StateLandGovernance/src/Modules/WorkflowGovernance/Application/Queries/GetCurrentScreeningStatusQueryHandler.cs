namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetCurrentScreeningStatusQueryHandler : IQueryHandler<GetCurrentScreeningStatusQuery, ScreeningResultDto?>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public GetCurrentScreeningStatusQueryHandler(ILeaseCaseRepository leaseCaseRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<ScreeningResultDto?> HandleAsync(
        GetCurrentScreeningStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(query.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null || leaseCase.LatestScreening == null)
        {
            return null;
        }

        return ScreeningMapper.ToDto(leaseCase.LatestScreening, leaseCase.CurrentVerifiedFactSnapshotId);
    }
}
