namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetLeaseCaseByIdQueryHandler : IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public GetLeaseCaseByIdQueryHandler(ILeaseCaseRepository leaseCaseRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<LeaseCaseDto> HandleAsync(
        GetLeaseCaseByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.LeaseCaseId == Guid.Empty)
        {
            throw new ValidationException(["Lease case identifier is required."]);
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(new LeaseCaseId(query.LeaseCaseId), cancellationToken)
            ?? throw new LeaseCaseNotFoundException(query.LeaseCaseId);

        return LeaseCaseMapper.ToDto(leaseCase);
    }
}
