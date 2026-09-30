namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetDocumentsByLeaseCaseIdQueryHandler : IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>>
{
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public GetDocumentsByLeaseCaseIdQueryHandler(
        IGovernedDocumentRepository documentRepository,
        ILeaseCaseRepository leaseCaseRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<IReadOnlyList<GovernedDocumentDto>> HandleAsync(
        GetDocumentsByLeaseCaseIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.LeaseCaseId == Guid.Empty)
        {
            throw new ValidationException(["LeaseCaseId cannot be empty."]);
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(new LeaseCaseId(query.LeaseCaseId), cancellationToken);
        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(query.LeaseCaseId);
        }

        var documents = await _documentRepository.GetByLeaseCaseIdAsync(new LeaseCaseId(query.LeaseCaseId), cancellationToken);

        return documents.Select(GovernedDocumentMapper.ToDto).ToList();
    }
}
