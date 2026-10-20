namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;

public sealed class ListLeaseCasesQueryHandler : IQueryHandler<ListLeaseCasesQuery, PagedLeaseCasesDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public ListLeaseCasesQueryHandler(ILeaseCaseRepository leaseCaseRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<PagedLeaseCasesDto> HandleAsync(
        ListLeaseCasesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, total) = await _leaseCaseRepository.ListAsync(
            query.Skip,
            query.Take,
            query.OwnerActorId,
            cancellationToken);

        var dtos = items.Select(LeaseCaseMapper.ToDto).ToList();

        return new PagedLeaseCasesDto(
            Items: dtos,
            TotalCount: total,
            Skip: query.Skip,
            Take: query.Take);
    }
}
